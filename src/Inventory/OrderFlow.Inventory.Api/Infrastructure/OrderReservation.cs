using OrderFlow.Contracts;
using OrderFlow.Inventory.Infrastructure.Persistence.Entities;

namespace OrderFlow.Inventory.Infrastructure;

/// <summary>
/// The "reserve every line or reserve nothing" decision for an incoming order, kept separate from
/// the database plumbing (locking, row writes) so it can be unit-tested without a database.
/// </summary>
public static class OrderReservation
{
    /// <summary>
    /// Merge duplicate SKU lines (summing quantities) and return them in a stable order, so that
    /// concurrent multi-line orders always take the row locks in the same sequence.
    /// </summary>
    public static IReadOnlyList<OrderLineContract> Consolidate(IEnumerable<OrderLineContract> lines) =>
        lines
            .GroupBy(line => line.Sku, StringComparer.OrdinalIgnoreCase)
            .Select(group => new OrderLineContract(
                group.Key,
                group.Sum(line => line.Quantity),
                group.First().UnitPrice))
            .OrderBy(line => line.Sku, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The rejection reason for the first line that cannot be fully reserved from the supplied
    /// stock, or <c>null</c> when the whole order fits. <paramref name="stock"/> returns the
    /// current row for a SKU, or <c>null</c> if the SKU is unknown.
    /// </summary>
    public static string? Shortfall(
        IEnumerable<OrderLineContract> consolidatedLines,
        Func<string, StockItemEntity?> stock)
    {
        foreach (var line in consolidatedLines)
        {
            if (!StockOperations.CanReserve(stock(line.Sku), line.Quantity))
            {
                return $"Insufficient stock for SKU {line.Sku}.";
            }
        }

        return null;
    }
}
