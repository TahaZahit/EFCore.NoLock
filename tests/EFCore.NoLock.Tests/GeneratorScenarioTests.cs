using System.Text.RegularExpressions;
using EFCore.NoLock;
using EFCore.NoLock.Core;
using EFCore.NoLock.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace EFCore.NoLock.Tests;

/// <summary>
/// The generator-based path (<c>optionsBuilder.UseNoLock()</c>) emits WITH (NOLOCK) during SQL
/// generation, so it is part of EF Core's generated SQL and shows up in EF Core's own command logs —
/// no capturing interceptor needed. Applied per query, only to those tagged via <c>.WithNoLock()</c>.
/// </summary>
[Collection("Northwind")]
public class GeneratorScenarioTests(ITestOutputHelper output)
{
    private const string ConnectionString =
        "Server=localhost,11433;Database=Northwind_EfCore;User Id=sa;Password=NoLock_Test123!;TrustServerCertificate=True;";

    private static readonly Regex TableRef = new(@"(?:FROM|JOIN)\s+\[\w+\]\s+AS\s+\[\w+\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private (NorthwindDbContext ctx, List<string> logs) Create()
    {
        var logs = new List<string>();
        var options = new DbContextOptionsBuilder<NorthwindDbContext>()
            .UseSqlServer(ConnectionString)
            .UseNoLock()
            .LogTo(logs.Add, LogLevel.Information)
            .Options;
        return (new NorthwindDbContext(options), logs);
    }

    private List<string> Executed(List<string> logs, string table) =>
        logs.Where(l => l.Contains("Executed DbCommand") && l.Contains($"[{table}]")).ToList();

    [Fact]
    public async Task EfLog_Shows_NoLock_For_Tagged_Query()
    {
        var (ctx, logs) = Create();
        await using var _ = ctx;

        await ctx.Products.AsNoTracking().Where(p => p.UnitPrice > 10).WithNoLock().ToListAsync();

        var executed = Executed(logs, "Products");
        output.WriteLine(string.Join("\n", executed));
        Assert.NotEmpty(executed);
        Assert.All(executed, l => Assert.Contains("WITH (NOLOCK)", l, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EfLog_Has_No_Hint_For_Untagged_Query()
    {
        var (ctx, logs) = Create();
        await using var _ = ctx;

        await ctx.Products.AsNoTracking().Where(p => p.UnitPrice > 10).ToListAsync();

        var executed = Executed(logs, "Products");
        Assert.NotEmpty(executed);
        Assert.All(executed, l => Assert.DoesNotContain("NOLOCK", l, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Generator_MultiJoin_All_Tables_Hinted()
    {
        var (ctx, logs) = Create();
        await using var _ = ctx;

        await ctx.Products.AsNoTracking()
            .Where(p => p.UnitPrice > 10)
            .Select(p => new { p.ProductName, Cat = p.Category!.CategoryName, Sup = p.Supplier!.CompanyName })
            .WithNoLock()
            .ToListAsync();

        var sql = Executed(logs, "Products").Single();
        output.WriteLine(sql);
        foreach (Match m in TableRef.Matches(sql))
        {
            var after = sql[(m.Index + m.Length)..].TrimStart();
            Assert.StartsWith("WITH (NOLOCK)", after, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("WITH (NOLOCK) WITH (NOLOCK)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.True(TableRef.Matches(sql).Count >= 3);
    }

    [Fact]
    public async Task Generator_SplitQuery_Every_Command_Hinted()
    {
        var (ctx, logs) = Create();
        await using var _ = ctx;

        await ctx.Orders.AsNoTracking()
            .Include(o => o.OrderDetails)
            .WithNoLock()
            .AsSplitQuery()
            .ToListAsync();

        var executed = logs.Where(l => l.Contains("Executed DbCommand") && (l.Contains("[Orders]") || l.Contains("[OrderDetails]"))).ToList();
        Assert.True(executed.Count >= 2, $"expected >=2 split commands, got {executed.Count}");
        foreach (var sql in executed)
            foreach (Match m in TableRef.Matches(sql))
            {
                var after = sql[(m.Index + m.Length)..].TrimStart();
                Assert.StartsWith("WITH (NOLOCK)", after, StringComparison.OrdinalIgnoreCase);
            }
    }

    [Fact]
    public async Task Generator_Does_Not_Hint_Writes()
    {
        var (ctx, logs) = Create();
        await using var _ = ctx;
        await using var tx = await ctx.Database.BeginTransactionAsync();

        var p = new Product { ProductName = "ZZ_GEN_WRITE", UnitPrice = 1m };
        ctx.Products.Add(p);
        await ctx.SaveChangesAsync();
        await tx.RollbackAsync();

        foreach (var sql in logs.Where(l => l.Contains("Executed DbCommand") && l.Contains("INSERT")))
        {
            output.WriteLine(sql);
            Assert.DoesNotContain("NOLOCK", sql, StringComparison.OrdinalIgnoreCase);
        }
    }
}
