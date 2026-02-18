using System.Text.RegularExpressions;
using EFCore.NoLock.Core;
using EFCore.NoLock.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace EFCore.NoLock.Tests;

/// <summary>
/// SQL-level verification: ensures every table in the query gets WITH (NOLOCK)
/// when .WithNoLock() is used.
/// </summary>
[Collection("Northwind")]
public partial class NoLockSqlVerificationTests(ITestOutputHelper output)
{
    private const string ConnectionString =
        "Server=localhost,11433;Database=Northwind;User Id=sa;Password=NoLock_Test123!;TrustServerCertificate=True;";

    private static (NorthwindDbContext context, SqlCaptureInterceptor spy) CreateWithSpy()
    {
        var spy = new SqlCaptureInterceptor();
        var noLock = new WithNoLockInterceptor();

        var options = new DbContextOptionsBuilder<NorthwindDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(noLock, spy) // NoLock transforms first, spy captures after
            .Options;

        return (new NorthwindDbContext(options), spy);
    }

    [Fact]
    public async Task Single_Table_Should_Have_NoLock()
    {
        // ARRANGE
        var (context, spy) = CreateWithSpy();
        await using var _ = context;

        // ACT
        try { await context.Products.WithNoLock().ToListAsync(); } catch { /* ignore mapping errors */ }

        // ASSERT
        var sql = spy.LastCommandText;
        output.WriteLine("=== CAPTURED SQL ===");
        output.WriteLine(sql);

        AssertAllTablesHaveNoLock(sql, ["Products"]);
    }

    [Fact]
    public async Task Two_Table_Join_Should_Both_Have_NoLock()
    {
        // ARRANGE
        var (context, spy) = CreateWithSpy();
        await using var _ = context;

        // ACT — Include causes a JOIN
        try
        {
            await context.Products
                .Include(p => p.Category)
                .WithNoLock()
                .ToListAsync();
        }
        catch { /* ignore mapping errors */ }

        // ASSERT
        var sql = spy.LastCommandText;
        output.WriteLine("=== CAPTURED SQL ===");
        output.WriteLine(sql);

        AssertAllTablesHaveNoLock(sql, ["Products", "Categories"]);
    }

    [Fact]
    public async Task Three_Table_Join_Should_All_Have_NoLock()
    {
        // ARRANGE
        var (context, spy) = CreateWithSpy();
        await using var _ = context;

        // ACT — Multiple Includes = multiple JOINs
        try
        {
            await context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .WithNoLock()
                .ToListAsync();
        }
        catch { /* ignore mapping errors */ }

        // ASSERT
        var sql = spy.LastCommandText;
        output.WriteLine("=== CAPTURED SQL ===");
        output.WriteLine(sql);

        AssertAllTablesHaveNoLock(sql, ["Products", "Categories", "Suppliers"]);
    }

    [Fact]
    public async Task Orders_With_Details_And_Products_Should_All_Have_NoLock()
    {
        // ARRANGE
        var (context, spy) = CreateWithSpy();
        await using var _ = context;

        // ACT — Deep include chain: Orders → OrderDetails → Product
        try
        {
            await context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .WithNoLock()
                .ToListAsync();
        }
        catch { /* ignore mapping errors */ }

        // ASSERT
        var sql = spy.LastCommandText;
        output.WriteLine("=== CAPTURED SQL ===");
        output.WriteLine(sql);

        AssertAllTablesHaveNoLock(sql, ["Orders", "Customers", "OrderDetails", "Products"]);
    }

    [Fact]
    public async Task Without_WithNoLock_Should_Have_No_NoLock_Hints()
    {
        // ARRANGE
        var (context, spy) = CreateWithSpy();
        await using var _ = context;

        // ACT — No .WithNoLock()
        try
        {
            await context.Products
                .Include(p => p.Category)
                .ToListAsync();
        }
        catch { /* ignore mapping errors */ }

        // ASSERT
        var sql = spy.LastCommandText;
        output.WriteLine("=== CAPTURED SQL (should have NO NOLOCK) ===");
        output.WriteLine(sql);

        Assert.DoesNotContain("NOLOCK", sql, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // Assertion helpers
    // =========================================================================

    /// <summary>
    /// Verifies that every expected table in the SQL has a WITH (NOLOCK) hint.
    /// Checks by counting FROM/JOIN [TableName] references and ensuring each
    /// is followed by WITH (NOLOCK).
    /// </summary>
    private void AssertAllTablesHaveNoLock(string sql, string[] expectedTables)
    {
        Assert.NotNull(sql);
        Assert.NotEmpty(sql);

        foreach (var table in expectedTables)
        {
            // Find all occurrences of [TableName] in FROM/JOIN clauses
            // Pattern: [TableName] AS [alias] WITH (NOLOCK)  — ScriptDom output format
            var pattern = $@"\[{Regex.Escape(table)}\]\s+AS\s+\[[a-zA-Z0-9_]+\]";
            var matches = Regex.Matches(sql, pattern, RegexOptions.IgnoreCase);

            output.WriteLine($"Table [{table}]: found {matches.Count} reference(s)");

            Assert.True(matches.Count > 0, $"Table [{table}] not found in SQL");

            foreach (Match match in matches)
            {
                // Check that this table reference is followed by WITH (NOLOCK)
                var afterTableRef = sql[(match.Index + match.Length)..];
                var trimmed = afterTableRef.TrimStart();

                output.WriteLine($"  Reference at pos {match.Index}: \"{match.Value}\"");
                output.WriteLine($"  Followed by: \"{trimmed[..Math.Min(30, trimmed.Length)]}...\"");

                Assert.StartsWith("WITH (NOLOCK)", trimmed, StringComparison.OrdinalIgnoreCase);
            }
        }

        output.WriteLine($"\n✅ All {expectedTables.Length} tables verified with WITH (NOLOCK)");
    }
}
