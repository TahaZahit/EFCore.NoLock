using System.Data.Common;
using System.Text.RegularExpressions;
using EFCore.NoLock;
using EFCore.NoLock.Core;
using EFCore.NoLock.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace EFCore.NoLock.Tests;

/// <summary>
/// Complex, kivi-api-shaped scenarios against a real SQL Server: multi-join, EXISTS/subquery,
/// GROUP BY, UNION and split queries, run through the same interceptor chain the app uses
/// (a tag-prepending interceptor in front of <see cref="WithNoLockInterceptor"/>). Verifies that
/// every physical table gets exactly one hint, that the hint is never doubled, and that neither the
/// NOLOCK tag nor the prepended APM tags are duplicated — including across repeated executions.
/// </summary>
[Collection("Northwind")]
public class ComplexScenarioTests(ITestOutputHelper output)
{
    private const string ConnectionString =
        "Server=localhost,11433;Database=Northwind_EfCore;User Id=sa;Password=NoLock_Test123!;TrustServerCertificate=True;";

    // Mirrors kivi-api's QueryTaggerInterceptor: prepends APM comment tags at execution time,
    // after WithNoLockInterceptor has already transformed the SQL at CommandCreated.
    private sealed class PrependTagsInterceptor : DbCommandInterceptor
    {
        private const string Cid = "--cid:11111111-1111-1111-1111-111111111111\n";
        private const string Uid = "--uid:22222222-2222-2222-2222-222222222222\n";
        private static void Prepend(DbCommand c)
        {
            if (c.CommandType != System.Data.CommandType.Text) return;
            var tags = "";
            if (!c.CommandText.Contains("--cid:")) tags += Cid;
            if (!c.CommandText.Contains("--uid:")) tags += Uid;
            if (tags.Length > 0) c.CommandText = tags + c.CommandText;
        }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r)
        { Prepend(c); return base.ReaderExecuting(c, e, r); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default)
        { Prepend(c); return base.ReaderExecutingAsync(c, e, r, ct); }
    }

    private sealed class CaptureAllInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r)
        { Commands.Add(c.CommandText); return base.ReaderExecuting(c, e, r); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default)
        { Commands.Add(c.CommandText); return base.ReaderExecutingAsync(c, e, r, ct); }
    }

    private (NorthwindDbContext ctx, CaptureAllInterceptor spy) Create()
    {
        var spy = new CaptureAllInterceptor();
        var options = new DbContextOptionsBuilder<NorthwindDbContext>()
            .UseSqlServer(ConnectionString)
            // Same order as kivi-api: tagger, then WithNoLock, plus a trailing spy to capture the final SQL.
            .AddInterceptors(new PrependTagsInterceptor())
            .AddInterceptors(new WithNoLockInterceptor())
            .AddInterceptors(spy)
            .Options;
        return (new NorthwindDbContext(options), spy);
    }

    // A physical table reference: [Table] AS [alias] immediately after FROM or JOIN. Anchoring on
    // FROM/JOIN avoids matching column aliases such as [c].[Name] AS [Tag].
    private static readonly Regex TableRef = new(@"(?:FROM|JOIN)\s+\[\w+\]\s+AS\s+\[\w+\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NoLockHint = new(@"WITH\s*\(NOLOCK\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private void AssertClean(string sql)
    {
        output.WriteLine(sql);
        output.WriteLine("----");

        // 1) every physical table reference is immediately followed by exactly one WITH (NOLOCK)
        foreach (Match m in TableRef.Matches(sql))
        {
            var after = sql[(m.Index + m.Length)..].TrimStart();
            Assert.StartsWith("WITH (NOLOCK)", after, StringComparison.OrdinalIgnoreCase);
        }

        // 2) the hint is never doubled on a single table
        Assert.DoesNotContain("WITH (NOLOCK) WITH (NOLOCK)", sql, StringComparison.OrdinalIgnoreCase);

        // 3) hint count matches table-reference count (no stray or missing hints)
        Assert.Equal(TableRef.Matches(sql).Count, NoLockHint.Matches(sql).Count);

        // 4) APM tags prepended by the tagger are not duplicated
        Assert.True(Regex.Matches(sql, "--cid:").Count <= 1, "cid tag duplicated");
        Assert.True(Regex.Matches(sql, "--uid:").Count <= 1, "uid tag duplicated");

        // 5) the internal NOLOCK marker is consumed by the transform, never left in or duplicated
        Assert.DoesNotContain(WithNoLockExtension.NoLockTag, sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MultiJoin_Projection_EachTable_SingleNoLock()
    {
        var (ctx, spy) = Create();
        await using var _ctx = ctx;

        _ = await ctx.Products.AsNoTracking()
            .Where(p => p.UnitPrice > 10)
            .Select(p => new { p.ProductName, Cat = p.Category!.CategoryName, Sup = p.Supplier!.CompanyName })
            .WithNoLock()
            .ToListAsync();

        Assert.NotEmpty(spy.Commands);
        foreach (var sql in spy.Commands) AssertClean(sql);
        Assert.Contains(spy.Commands, s => s.Contains("[Products]") && s.Contains("[Categories]") && s.Contains("[Suppliers]"));
    }

    [Fact]
    public async Task ExistsSubquery_Outer_And_Inner_NoLock()
    {
        var (ctx, spy) = Create();
        await using var _ctx = ctx;

        _ = await ctx.Categories.AsNoTracking()
            .Where(c => c.Products.Any(p => p.UnitPrice > 20))
            .Select(c => c.CategoryName)
            .WithNoLock()
            .ToListAsync();

        Assert.NotEmpty(spy.Commands);
        foreach (var sql in spy.Commands) AssertClean(sql);
        Assert.Contains(spy.Commands, s => s.Contains("EXISTS") && s.Contains("[Categories]") && s.Contains("[Products]"));
    }

    [Fact]
    public async Task GroupBy_Aggregate_NoLock()
    {
        var (ctx, spy) = Create();
        await using var _ctx = ctx;

        _ = await ctx.OrderDetails.AsNoTracking()
            .GroupBy(od => od.ProductID)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity), Lines = g.Count() })
            .WithNoLock()
            .ToListAsync();

        Assert.NotEmpty(spy.Commands);
        foreach (var sql in spy.Commands) AssertClean(sql);
        Assert.Contains(spy.Commands, s => s.Contains("GROUP BY") && s.Contains("[OrderDetails]"));
    }

    [Fact]
    public async Task Union_BothBranches_NoLock()
    {
        var (ctx, spy) = Create();
        await using var _ctx = ctx;

        var cheap = ctx.Products.AsNoTracking().Where(p => p.UnitPrice < 10).Select(p => p.ProductName);
        var dear = ctx.Products.AsNoTracking().Where(p => p.UnitPrice > 100).Select(p => p.ProductName);
        _ = await cheap.Concat(dear).WithNoLock().ToListAsync();

        Assert.NotEmpty(spy.Commands);
        foreach (var sql in spy.Commands) AssertClean(sql);
        // two Products references (one per UNION branch), two hints
        Assert.Contains(spy.Commands, s => NoLockHint.Matches(s).Count >= 2);
    }

    [Fact]
    public async Task SplitQuery_MultiCommand_EveryCommand_NoLock()
    {
        var (ctx, spy) = Create();
        await using var _ctx = ctx;

        _ = await ctx.Orders.AsNoTracking()
            .Include(o => o.OrderDetails)
            .WithNoLock()
            .AsSplitQuery()
            .ToListAsync();

        Assert.True(spy.Commands.Count >= 2, $"expected split into >=2 commands, got {spy.Commands.Count}");
        foreach (var sql in spy.Commands) AssertClean(sql);
    }

    [Fact]
    public async Task RepeatedExecution_SameQuery_NoDoubledHint()
    {
        var (ctx, spy) = Create();
        await using var _ctx = ctx;

        var q = ctx.Products.AsNoTracking()
            .Where(p => p.UnitPrice > 10)
            .Select(p => new { p.ProductName, Cat = p.Category!.CategoryName })
            .WithNoLock();

        for (var i = 0; i < 3; i++) _ = await q.ToListAsync();

        Assert.True(spy.Commands.Count >= 3);
        foreach (var sql in spy.Commands) AssertClean(sql);
    }
}
