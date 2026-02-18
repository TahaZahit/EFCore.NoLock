using EFCore.NoLock.Core;
using EFCore.NoLock.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace EFCore.NoLock.Tests;

/// <summary>
/// Integration tests against a real SQL Server (Northwind database in Docker).
/// Run 'docker compose up -d' and seed the database before executing these tests.
/// </summary>
[Collection("Northwind")]
public class NorthwindIntegrationTests(ITestOutputHelper output)
{
    private const string ConnectionString =
        "Server=localhost,11433;Database=Northwind;User Id=sa;Password=NoLock_Test123!;TrustServerCertificate=True;";

    [Fact]
    public async Task Should_Read_Products_With_NoLock()
    {
        // ARRANGE
        await using var context = NorthwindDbContext.Create(ConnectionString);

        // ACT
        var products = await context.Products
            .WithNoLock()
            .ToListAsync();

        // LOGGING
        output.WriteLine($"Loaded {products.Count} products");
        foreach (var p in products)
            output.WriteLine($"  [{p.ProductID}] {p.ProductName} - ${p.UnitPrice}");

        // ASSERT
        Assert.NotEmpty(products);
        Assert.Contains(products, p => p.ProductName == "Chai");
    }

    [Fact]
    public async Task Should_Read_Orders_With_Include_With_NoLock()
    {
        // ARRANGE
        await using var context = NorthwindDbContext.Create(ConnectionString);

        // ACT
        var orders = await context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
            .WithNoLock()
            .ToListAsync();

        // LOGGING
        output.WriteLine($"Loaded {orders.Count} orders");
        foreach (var o in orders)
        {
            output.WriteLine($"  Order #{o.OrderID} - Customer: {o.Customer?.CompanyName} - {o.OrderDetails.Count} items");
            foreach (var od in o.OrderDetails)
                output.WriteLine($"    {od.Product.ProductName} x{od.Quantity} @ ${od.UnitPrice}");
        }

        // ASSERT
        Assert.NotEmpty(orders);
        Assert.All(orders, o => Assert.NotNull(o.Customer));
        Assert.Contains(orders, o => o.OrderDetails.Count > 0);
    }

    [Fact]
    public async Task Should_Read_Products_Without_NoLock_Works_Normally()
    {
        // ARRANGE
        await using var context = NorthwindDbContext.Create(ConnectionString);

        // ACT — No .WithNoLock(), should still work normally
        var products = await context.Products
            .Where(p => !p.Discontinued)
            .ToListAsync();

        // ASSERT
        Assert.NotEmpty(products);
    }

    [Fact]
    public async Task Should_Filter_And_Join_With_NoLock()
    {
        // ARRANGE
        await using var context = NorthwindDbContext.Create(ConnectionString);

        // ACT — Complex query with joins and filters
        var results = await context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Where(p => p.UnitPrice > 15)
            .WithNoLock()
            .ToListAsync();

        // LOGGING
        output.WriteLine($"Products with UnitPrice > 15: {results.Count}");
        foreach (var r in results)
            output.WriteLine($"  {r.ProductName} (Category: {r.Category?.CategoryName}, Supplier: {r.Supplier?.CompanyName})");

        // ASSERT
        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.True(p.UnitPrice > 15));
    }
}
