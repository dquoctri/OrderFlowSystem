namespace OrderFlow.Orders.Infrastructure.Persistence.Entities;

public sealed class OrderLineEntity
{
    public long Id { get; set; }
    public Guid OrderId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public OrderEntity Order { get; set; } = null!;
}
