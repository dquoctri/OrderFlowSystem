using OrderFlow.Inventory.Infrastructure.Persistence.Entities;

namespace OrderFlow.Inventory.Infrastructure;

/// <summary>
/// The stock quantity rules, as pure functions over a <see cref="StockItemEntity"/>. The database
/// row lock (<c>SELECT … FOR UPDATE</c> in <c>InventoryEventConsumer.LockStockAsync</c>) is what
/// makes concurrent callers safe; these methods are the arithmetic and the invariants that run
/// once a caller holds the lock. Every method keeps <c>available = QuantityOnHand - QuantityReserved &gt;= 0</c>.
/// </summary>
public static class StockOperations
{
    public static int Available(StockItemEntity stock) => stock.QuantityOnHand - stock.QuantityReserved;

    /// <summary>True when <paramref name="quantity"/> units can be reserved from <paramref name="stock"/> right now.</summary>
    public static bool CanReserve(StockItemEntity? stock, int quantity) =>
        stock is not null && quantity > 0 && Available(stock) >= quantity;

    /// <summary>Hold <paramref name="quantity"/> units. Throws if they do not fit.</summary>
    public static void Reserve(StockItemEntity stock, int quantity)
    {
        RequirePositive(quantity);
        if (Available(stock) < quantity)
        {
            throw new InvalidOperationException(
                $"SKU {stock.Sku}: cannot reserve {quantity}; only {Available(stock)} available.");
        }

        stock.QuantityReserved += quantity;
    }

    /// <summary>Compensation: give <paramref name="quantity"/> reserved units back to the available pool.</summary>
    public static void Release(StockItemEntity stock, int quantity)
    {
        RequirePositive(quantity);
        if (stock.QuantityReserved < quantity)
        {
            throw new InvalidOperationException(
                $"SKU {stock.Sku}: cannot release {quantity}; only {stock.QuantityReserved} reserved.");
        }

        stock.QuantityReserved -= quantity;
    }

    /// <summary>Settlement after payment: permanently remove <paramref name="quantity"/> units from on-hand and reserved.</summary>
    public static void Consume(StockItemEntity stock, int quantity)
    {
        RequirePositive(quantity);
        if (stock.QuantityReserved < quantity || stock.QuantityOnHand < quantity)
        {
            throw new InvalidOperationException(
                $"SKU {stock.Sku}: cannot consume {quantity} (on hand {stock.QuantityOnHand}, reserved {stock.QuantityReserved}).");
        }

        stock.QuantityOnHand -= quantity;
        stock.QuantityReserved -= quantity;
    }

    private static void RequirePositive(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
        }
    }
}
