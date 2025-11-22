namespace EFCore.NoLock.Tests.Helpers;


public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<OrderLine> Lines { get; set; } = new();
}

public class OrderLine
{
    public int Id { get; set; }
    public string Product { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
}