using OrderFlow.Contracts;
using OrderFlow.Inventory.Infrastructure;
using OrderFlow.Inventory.Infrastructure.Persistence.Entities;

namespace Inventory.Tests;

public sealed class OrderReservationTests
{
    private static OrderLineContract Line(string sku, int quantity, decimal unitPrice = 10m) =>
        new(sku, quantity, unitPrice);

    private static Func<string, StockItemEntity?> StockOf(params (string Sku, int OnHand, int Reserved)[] rows)
    {
        var map = rows.ToDictionary(
            row => row.Sku,
            row => new StockItemEntity { Sku = row.Sku, QuantityOnHand = row.OnHand, QuantityReserved = row.Reserved },
            StringComparer.OrdinalIgnoreCase);
        return sku => map.GetValueOrDefault(sku);
    }

    [Fact]
    public void Consolidate_WithDuplicateSkuLines_SumsTheQuantities()
    {
        // Arrange
        var lines = new[] { Line("WIDGET-01", 2), Line("WIDGET-01", 3) };

        // Act
        var consolidated = OrderReservation.Consolidate(lines);

        // Assert
        var single = Assert.Single(consolidated);
        Assert.Equal("WIDGET-01", single.Sku);
        Assert.Equal(5, single.Quantity);
    }

    [Fact]
    public void Consolidate_ReturnsLinesInAStableSkuOrder()
    {
        // Arrange
        var lines = new[] { Line("WIDGET-02", 1), Line("WIDGET-01", 1) };

        // Act
        var consolidated = OrderReservation.Consolidate(lines);

        // Assert
        Assert.Equal(new[] { "WIDGET-01", "WIDGET-02" }, consolidated.Select(line => line.Sku));
    }

    [Fact]
    public void Shortfall_WhenEveryLineFits_ReturnsNull()
    {
        // Arrange
        var lines = new[] { Line("WIDGET-01", 2), Line("WIDGET-02", 1) };
        var stock = StockOf(("WIDGET-01", 10, 0), ("WIDGET-02", 5, 0));

        // Act
        var reason = OrderReservation.Shortfall(lines, stock);

        // Assert
        Assert.Null(reason);
    }

    [Fact]
    public void Shortfall_ForTheLastAvailableUnit_ReturnsNull()
    {
        // Arrange
        var lines = new[] { Line("WIDGET-01", 1) };
        var stock = StockOf(("WIDGET-01", 1, 0));

        // Act
        var reason = OrderReservation.Shortfall(lines, stock);

        // Assert
        Assert.Null(reason);
    }

    [Fact]
    public void Shortfall_WhenOneLineIsShort_NamesThatSku()
    {
        // Arrange
        var lines = new[] { Line("WIDGET-01", 2), Line("WIDGET-02", 9) };
        var stock = StockOf(("WIDGET-01", 10, 0), ("WIDGET-02", 5, 0));

        // Act
        var reason = OrderReservation.Shortfall(lines, stock);

        // Assert
        Assert.Equal("Insufficient stock for SKU WIDGET-02.", reason);
    }

    [Fact]
    public void Shortfall_WhenAlreadyReservedLeavesTooLittle_ReturnsAReason()
    {
        // Arrange — 1 on hand, already reserved, so the last unit is gone.
        var lines = new[] { Line("WIDGET-01", 1) };
        var stock = StockOf(("WIDGET-01", 1, 1));

        // Act
        var reason = OrderReservation.Shortfall(lines, stock);

        // Assert
        Assert.Equal("Insufficient stock for SKU WIDGET-01.", reason);
    }

    [Fact]
    public void Shortfall_ForAnUnknownSku_ReturnsAReason()
    {
        // Arrange
        var lines = new[] { Line("WIDGET-99", 1) };
        var stock = StockOf(("WIDGET-01", 10, 0));

        // Act
        var reason = OrderReservation.Shortfall(lines, stock);

        // Assert
        Assert.Equal("Insufficient stock for SKU WIDGET-99.", reason);
    }
}
