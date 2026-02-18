using EFCore.NoLock.Core;
using EFCore.NoLock.LinqToDb;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;
using LinqToDB.Mapping;
using Xunit.Abstractions;

namespace EFCore.NoLock.LinqToDb.Tests;

/// <summary>
/// Integration tests for LinqToDB interceptor against a real SQL Server (Northwind in Docker).
/// Run 'docker compose up -d' and seed the database before executing these tests.
/// </summary>
[Collection("Northwind")]
public class NorthwindLinqToDbIntegrationTests(ITestOutputHelper output)
{
    private const string ConnectionString =
        "Server=localhost,11433;Database=Northwind;User Id=sa;Password=NoLock_Test123!;TrustServerCertificate=True;";

    private static DataConnection CreateConnection()
    {
        var options = new DataOptions()
            .UseSqlServer(ConnectionString)
            .UseInterceptor(new LinqToDbWithNoLockInterceptor());

        return new DataConnection(options);
    }

    [Fact]
    public void Should_Read_Products_With_NoLock()
    {
        // ARRANGE
        using var db = CreateConnection();

        // ACT
        var products = db.GetTable<NwProduct>()
            .WithNoLock()
            .ToList();

        // LOGGING
        output.WriteLine($"Loaded {products.Count} products");
        foreach (var p in products)
            output.WriteLine($"  [{p.ProductID}] {p.ProductName} - ${p.UnitPrice}");

        // ASSERT
        Assert.NotEmpty(products);
        Assert.Contains(products, p => p.ProductName == "Chai");
    }

    [Fact]
    public void Should_Read_Orders_With_NoLock()
    {
        // ARRANGE
        using var db = CreateConnection();

        // ACT
        var orders = db.GetTable<NwOrder>()
            .WithNoLock()
            .ToList();

        // LOGGING
        output.WriteLine($"Loaded {orders.Count} orders");
        foreach (var o in orders)
            output.WriteLine($"  Order #{o.OrderID} - Customer: {o.CustomerID} - Ship: {o.ShipCity}, {o.ShipCountry}");

        // ASSERT
        Assert.NotEmpty(orders);
    }

    [Fact]
    public void Should_Filter_Products_With_NoLock()
    {
        // ARRANGE
        using var db = CreateConnection();

        // ACT
        var results = db.GetTable<NwProduct>()
            .Where(p => p.UnitPrice > 15)
            .WithNoLock()
            .ToList();

        // LOGGING
        output.WriteLine($"Products with UnitPrice > 15: {results.Count}");
        foreach (var r in results)
            output.WriteLine($"  {r.ProductName} - ${r.UnitPrice}");

        // ASSERT
        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.True(p.UnitPrice > 15));
    }

    [Fact]
    public void Should_Read_Without_NoLock_Works_Normally()
    {
        // ARRANGE
        using var db = CreateConnection();

        // ACT — No .WithNoLock(), should still work normally
        var products = db.GetTable<NwProduct>()
            .Where(p => !p.Discontinued)
            .ToList();

        // ASSERT
        Assert.NotEmpty(products);
    }
}

// LinqToDB Northwind mapping models
[Table("Products")]
public class NwProduct
{
    [PrimaryKey, Identity]
    public int ProductID { get; set; }

    [Column, NotNull]
    public string ProductName { get; set; } = null!;

    [Column, Nullable]
    public int? SupplierID { get; set; }

    [Column, Nullable]
    public int? CategoryID { get; set; }

    [Column, Nullable]
    public decimal? UnitPrice { get; set; }

    [Column, Nullable]
    public short? UnitsInStock { get; set; }

    [Column, NotNull]
    public bool Discontinued { get; set; }
}

[Table("Orders")]
public class NwOrder
{
    [PrimaryKey, Identity]
    public int OrderID { get; set; }

    [Column, Nullable]
    public string? CustomerID { get; set; }

    [Column, Nullable]
    public DateTime? OrderDate { get; set; }

    [Column, Nullable]
    public string? ShipCity { get; set; }

    [Column, Nullable]
    public string? ShipCountry { get; set; }
}

[Table("OrderDetails")]
public class NwOrderDetail
{
    [PrimaryKey]
    public int OrderID { get; set; }

    [PrimaryKey]
    public int ProductID { get; set; }

    [Column, NotNull]
    public decimal UnitPrice { get; set; }

    [Column, NotNull]
    public short Quantity { get; set; }

    [Column, NotNull]
    public float Discount { get; set; }
}
