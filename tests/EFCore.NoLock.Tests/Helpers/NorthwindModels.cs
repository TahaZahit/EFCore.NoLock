using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EFCore.NoLock.Tests.Helpers;

[Table("Categories")]
public class Category
{
    [Key]
    public int CategoryID { get; set; }
    public string CategoryName { get; set; } = null!;
    public string? Description { get; set; }
    public ICollection<Product> Products { get; set; } = [];
}

[Table("Suppliers")]
public class Supplier
{
    [Key]
    public int SupplierID { get; set; }
    public string CompanyName { get; set; } = null!;
    public string? ContactName { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public ICollection<Product> Products { get; set; } = [];
}

[Table("Products")]
public class Product
{
    [Key]
    public int ProductID { get; set; }
    public string ProductName { get; set; } = null!;
    public int? SupplierID { get; set; }
    public int? CategoryID { get; set; }

    [Column(TypeName = "money")]
    public decimal? UnitPrice { get; set; }
    public short? UnitsInStock { get; set; }
    public bool Discontinued { get; set; }

    [ForeignKey(nameof(SupplierID))]
    public Supplier? Supplier { get; set; }

    [ForeignKey(nameof(CategoryID))]
    public Category? Category { get; set; }

    public ICollection<OrderDetail> OrderDetails { get; set; } = [];
}

[Table("Customers")]
public class Customer
{
    [Key]
    [Column(TypeName = "nchar(5)")]
    public string CustomerID { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? ContactName { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public ICollection<NorthwindOrder> Orders { get; set; } = [];
}

[Table("Orders")]
public class NorthwindOrder
{
    [Key]
    public int OrderID { get; set; }

    [Column(TypeName = "nchar(5)")]
    public string? CustomerID { get; set; }
    public DateTime? OrderDate { get; set; }
    public string? ShipCity { get; set; }
    public string? ShipCountry { get; set; }

    [ForeignKey(nameof(CustomerID))]
    public Customer? Customer { get; set; }

    public ICollection<OrderDetail> OrderDetails { get; set; } = [];
}

[Table("OrderDetails")]
public class OrderDetail
{
    public int OrderID { get; set; }
    public int ProductID { get; set; }

    [Column(TypeName = "money")]
    public decimal UnitPrice { get; set; }
    public short Quantity { get; set; }
    public float Discount { get; set; }

    [ForeignKey(nameof(OrderID))]
    public NorthwindOrder Order { get; set; } = null!;

    [ForeignKey(nameof(ProductID))]
    public Product Product { get; set; } = null!;
}
