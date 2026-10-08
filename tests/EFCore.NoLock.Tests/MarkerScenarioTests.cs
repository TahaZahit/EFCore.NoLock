using System.Data.Common;
using System.Text.RegularExpressions;
using EFCore.NoLock.Core;
using EFCore.NoLock.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace EFCore.NoLock.Tests;

/// <summary>
/// Regression tests for the tag-marker model: the hint must travel with the query through
/// SelectMany composition, through every command of an AsSplitQuery, and across repeated
/// executions of the same reused IQueryable (the scenario the old one-shot flag broke).
/// </summary>
[Collection("Northwind")]
public class MarkerScenarioTests(ITestOutputHelper output)
{
    private const string ConnectionString =
        "Server=localhost,11433;Database=Northwind_EfCore;User Id=sa;Password=NoLock_Test123!;TrustServerCertificate=True;";

    private sealed class CaptureAllInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static (NorthwindDbContext ctx, CaptureAllInterceptor spy) Create()
    {
        var spy = new CaptureAllInterceptor();
        var options = new DbContextOptionsBuilder<NorthwindDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(new WithNoLockInterceptor(), spy)
            .Options;
        return (new NorthwindDbContext(options), spy);
    }

    private static readonly Regex TableRef = new(@"\]\s+AS\s+\[\w+\]", RegexOptions.Compiled);

    private void AssertEveryTableHasNoLock(string sql)
    {
        output.WriteLine(sql);
        foreach (Match m in TableRef.Matches(sql))
        {
            var after = sql[(m.Index + m.Length)..].TrimStart();
            Assert.StartsWith("WITH (NOLOCK)", after, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("WITH (NOLOCK)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelectMany_Into_Collection_Keeps_NoLock()
    {
        var (ctx, spy) = Create();
        await using var _ = ctx;

        // Base query is tagged once, then reshaped via SelectMany — the prod "tags" query shape.
        var baseQ = ctx.Categories.AsNoTracking().Where(c => c.CategoryID > 0).WithNoLock();
        await baseQ.SelectMany(c => c.Products).Select(p => p.ProductName).Distinct().ToListAsync();

        Assert.NotEmpty(spy.Commands);
        foreach (var sql in spy.Commands)
            AssertEveryTableHasNoLock(sql);
    }

    [Fact]
    public async Task SplitQuery_Applies_NoLock_To_Every_Command()
    {
        var (ctx, spy) = Create();
        await using var _ = ctx;

        await ctx.Customers.AsNoTracking()
            .Include(c => c.Orders)
            .WithNoLock()
            .AsSplitQuery()
            .ToListAsync();

        // A split query fires more than one command; the old one-shot flag only tagged the first.
        Assert.True(spy.Commands.Count >= 2, $"expected split into >=2 commands, got {spy.Commands.Count}");
        foreach (var sql in spy.Commands)
            AssertEveryTableHasNoLock(sql);
    }

    [Fact]
    public async Task Reused_Query_Keeps_NoLock_On_Every_Execution()
    {
        var (ctx, spy) = Create();
        await using var _ = ctx;

        // One .WithNoLock(), reused for several executions — the GetMetasFromQuery pattern.
        var baseQ = ctx.Categories.AsNoTracking().Where(c => c.CategoryID > 0).WithNoLock();
        await baseQ.Select(c => c.CategoryName).Distinct().ToListAsync();
        await baseQ.Where(c => c.CategoryName != null).Select(c => c.CategoryID).ToListAsync();
        await baseQ.SelectMany(c => c.Products).Select(p => p.ProductID).Distinct().ToListAsync();

        Assert.True(spy.Commands.Count >= 3, $"expected >=3 executions, got {spy.Commands.Count}");
        foreach (var sql in spy.Commands)
            AssertEveryTableHasNoLock(sql);
    }
}
