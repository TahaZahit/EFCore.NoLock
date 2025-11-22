using Microsoft.EntityFrameworkCore;

namespace EFCore.NoLock.Tests.Helpers;

public class TestDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderLine> OrderLines { get; set; }
    
    public static (TestDbContext context, SqlCaptureInterceptor sqlSpy) PrepareSql()
    {
        TestDbContext? context = null;
        try
        {
            var sqlSpy = new SqlCaptureInterceptor();
            var myNoLockInterceptor = new WithNoLockInterceptor();
            var fakeConnection = new FakeDbConnection();

            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(fakeConnection)
                .AddInterceptors(myNoLockInterceptor, sqlSpy)
                .Options;

            context = new TestDbContext(options);
            return (context,sqlSpy);
        }
        catch
        {
            context?.Dispose();
            throw;
        }
    }
}

