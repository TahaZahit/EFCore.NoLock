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
/// Write-path safety: INSERT/UPDATE/DELETE must never receive a NOLOCK hint and must execute
/// unchanged, even though the interceptor now runs at CommandCreated for every command type and
/// even with the APM tagger prepending comments. Two guards make this hold: the NOLOCK tag is only
/// present on queries built with <c>.WithNoLock()</c>, and the SQL visitor only hints tables inside
/// SELECT statements. All writes run inside a transaction that is rolled back, so the shared fixture
/// is never mutated.
/// </summary>
[Collection("Northwind")]
public class DmlScenarioTests(ITestOutputHelper output)
{
    private const string ConnectionString =
        "Server=localhost,11433;Database=Northwind_EfCore;User Id=sa;Password=NoLock_Test123!;TrustServerCertificate=True;";

    private sealed class PrependTagsInterceptor : DbCommandInterceptor
    {
        private static void Prepend(DbCommand c)
        {
            if (c.CommandType != System.Data.CommandType.Text) return;
            if (!c.CommandText.Contains("--cid:")) c.CommandText = "--cid:11111111-1111-1111-1111-111111111111\n" + c.CommandText;
        }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r) { Prepend(c); return base.ReaderExecuting(c, e, r); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default) { Prepend(c); return base.ReaderExecutingAsync(c, e, r, ct); }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand c, CommandEventData e, InterceptionResult<int> r) { Prepend(c); return base.NonQueryExecuting(c, e, r); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<int> r, CancellationToken ct = default) { Prepend(c); return base.NonQueryExecutingAsync(c, e, r, ct); }
        public override InterceptionResult<object> ScalarExecuting(DbCommand c, CommandEventData e, InterceptionResult<object> r) { Prepend(c); return base.ScalarExecuting(c, e, r); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default) { Prepend(c); return base.ScalarExecutingAsync(c, e, r, ct); }
    }

    private sealed class CaptureAllInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        private void Add(DbCommand c) => Commands.Add(c.CommandText);
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r) { Add(c); return base.ReaderExecuting(c, e, r); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default) { Add(c); return base.ReaderExecutingAsync(c, e, r, ct); }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand c, CommandEventData e, InterceptionResult<int> r) { Add(c); return base.NonQueryExecuting(c, e, r); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<int> r, CancellationToken ct = default) { Add(c); return base.NonQueryExecutingAsync(c, e, r, ct); }
        public override InterceptionResult<object> ScalarExecuting(DbCommand c, CommandEventData e, InterceptionResult<object> r) { Add(c); return base.ScalarExecuting(c, e, r); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default) { Add(c); return base.ScalarExecutingAsync(c, e, r, ct); }
    }

    private (NorthwindDbContext ctx, CaptureAllInterceptor spy) Create()
    {
        var spy = new CaptureAllInterceptor();
        var options = new DbContextOptionsBuilder<NorthwindDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(new PrependTagsInterceptor())
            .AddInterceptors(new WithNoLockInterceptor())
            .AddInterceptors(spy)
            .Options;
        return (new NorthwindDbContext(options), spy);
    }

    private static readonly Regex Dml = new(@"\b(INSERT|UPDATE|DELETE|MERGE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NoLockHint = new(@"WITH\s*\(NOLOCK\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // A write command must never receive the WITH (NOLOCK) table hint. (A harmless leading marker
    // comment may survive on a bulk DML that WithNoLock was misapplied to; only the real hint matters.)
    private void AssertNoHintOnWrites(IEnumerable<string> commands)
    {
        foreach (var sql in commands)
        {
            if (!Dml.IsMatch(sql)) continue;
            output.WriteLine(sql);
            output.WriteLine("----");
            Assert.False(NoLockHint.IsMatch(sql), "write command received a NOLOCK hint");
        }
    }

    [Fact]
    public async Task Insert_Update_Delete_Via_SaveChanges_Are_Untouched()
    {
        var (ctx, spy) = Create();
        await using var _ctx = ctx;
        await using var tx = await ctx.Database.BeginTransactionAsync();

        var p = new Product { ProductName = "ZZ_NOLOCK_WRITE_TEST", UnitPrice = 1m, Discontinued = false };
        ctx.Products.Add(p);
        var inserted = await ctx.SaveChangesAsync();          // INSERT (+ identity SELECT back)
        Assert.Equal(1, inserted);
        Assert.True(p.ProductID > 0);

        p.UnitPrice = 2m;
        Assert.Equal(1, await ctx.SaveChangesAsync());        // UPDATE

        ctx.Products.Remove(p);
        Assert.Equal(1, await ctx.SaveChangesAsync());        // DELETE

        await tx.RollbackAsync();

        AssertNoHintOnWrites(spy.Commands);
        Assert.Contains(spy.Commands, s => s.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(spy.Commands, s => s.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(spy.Commands, s => s.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Read_With_NoLock_And_Write_Without_In_Same_Context()
    {
        var (ctx, spy) = Create();
        await using var _ctx = ctx;
        await using var tx = await ctx.Database.BeginTransactionAsync();

        // read side: NOLOCK expected
        _ = await ctx.Products.AsNoTracking().Where(p => p.UnitPrice > 10).WithNoLock().ToListAsync();
        // write side: no NOLOCK
        var p = new Product { ProductName = "ZZ_MIXED", UnitPrice = 5m };
        ctx.Products.Add(p);
        await ctx.SaveChangesAsync();
        await tx.RollbackAsync();

        AssertNoHintOnWrites(spy.Commands);
        Assert.Contains(spy.Commands, s => s.Contains("[Products]") && s.Contains("WITH (NOLOCK)", StringComparison.OrdinalIgnoreCase) && !Dml.IsMatch(s));
    }

    [Fact]
    public async Task Scalar_Count_With_NoLock_Gets_Hint()
    {
        var (ctx, spy) = Create();
        await using var _ctx = ctx;

        _ = await ctx.Products.AsNoTracking().Where(p => p.UnitPrice > 10).WithNoLock().CountAsync();

        Assert.Contains(spy.Commands, s => s.Contains("COUNT", StringComparison.OrdinalIgnoreCase) && s.Contains("WITH (NOLOCK)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteUpdate_With_NoLock_Does_Not_Hint_The_Update_Target()
    {
        var (ctx, spy) = Create();
        await using var _ctx = ctx;
        await using var tx = await ctx.Database.BeginTransactionAsync();

        // Even if .WithNoLock() is (mis)applied to a bulk update, the UPDATE target must not get a
        // hint — the visitor only touches tables inside SELECT statements — and it must not error.
        var affected = await ctx.Products
            .Where(p => p.ProductName == "ZZ_DOES_NOT_EXIST")
            .WithNoLock()
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UnitPrice, x => x.UnitPrice));
        Assert.Equal(0, affected);

        await tx.RollbackAsync();
        AssertNoHintOnWrites(spy.Commands);
    }

    [Fact]
    public async Task ExecuteDelete_With_NoLock_Does_Not_Hint_The_Delete_Target()
    {
        var (ctx, spy) = Create();
        await using var _ctx = ctx;
        await using var tx = await ctx.Database.BeginTransactionAsync();

        var affected = await ctx.Products
            .Where(p => p.ProductName == "ZZ_DOES_NOT_EXIST")
            .WithNoLock()
            .ExecuteDeleteAsync();
        Assert.Equal(0, affected);

        await tx.RollbackAsync();
        AssertNoHintOnWrites(spy.Commands);
    }

    [Fact]
    public async Task Insert_Select_With_Marker_Stays_Valid_And_Target_Not_Hinted()
    {
        // Even a marker-carrying INSERT...SELECT must stay valid: the INSERT target is never hinted
        // (the visitor only touches tables inside a SELECT statement), and the command must not error.
        var (ctx, spy) = Create();
        await using var _ctx = ctx;
        await using var tx = await ctx.Database.BeginTransactionAsync();

        var affected = await ctx.Database.ExecuteSqlRawAsync(
            "-- " + WithNoLockExtension.NoLockTag + "\nINSERT INTO [Categories] ([CategoryName]) SELECT TOP 0 [CategoryName] FROM [Categories] AS [c]");
        Assert.Equal(0, affected);
        await tx.RollbackAsync();

        var cmd = spy.Commands.FirstOrDefault(s => s.Contains("INSERT INTO [Categories]"));
        Assert.NotNull(cmd);
        output.WriteLine(cmd);
        Assert.DoesNotMatch(@"INSERT INTO \[Categories\][^\n]*WITH \(NOLOCK\)", cmd);
    }
}
