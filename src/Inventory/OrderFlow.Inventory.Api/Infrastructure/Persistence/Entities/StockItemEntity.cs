namespace OrderFlow.Inventory.Infrastructure.Persistence.Entities;

public sealed class StockItemEntity
{
    public string Sku { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
}
