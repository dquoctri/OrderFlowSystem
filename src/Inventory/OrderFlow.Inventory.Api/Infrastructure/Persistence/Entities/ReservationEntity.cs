namespace OrderFlow.Inventory.Infrastructure.Persistence.Entities;

public sealed class ReservationEntity
{
    public long Id { get; set; }
    public Guid OrderId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
