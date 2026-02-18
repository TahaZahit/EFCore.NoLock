using Microsoft.EntityFrameworkCore;

namespace EFCore.NoLock.Tests.Helpers;

public class NorthwindDbContext(DbContextOptions<NorthwindDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<NorthwindOrder> Orders { get; set; }
    public DbSet<OrderDetail> OrderDetails { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderDetail>()
            .HasKey(od => new { od.OrderID, od.ProductID });
    }

    public static NorthwindDbContext Create(string connectionString)
    {
        var interceptor = new WithNoLockInterceptor();
        var options = new DbContextOptionsBuilder<NorthwindDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new NorthwindDbContext(options);
    }
}
