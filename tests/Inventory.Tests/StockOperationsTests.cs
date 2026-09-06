using OrderFlow.Inventory.Infrastructure;
using OrderFlow.Inventory.Infrastructure.Persistence.Entities;

namespace Inventory.Tests;

/// <summary>
/// Fast, infrastructure-free coverage of the stock arithmetic and invariants. The database row
/// lock that makes concurrent callers safe is exercised separately by
/// <see cref="StockReservationConcurrencyTests"/> (Testcontainers).
/// </summary>
public sealed class StockOperationsTests
{
    private static StockItemEntity Stock(int onHand, int reserved) =>
        new() { Sku = "WIDGET-01", QuantityOnHand = onHand, QuantityReserved = reserved };

    [Theory]
    [InlineData(10, 0, 10)]
    [InlineData(10, 4, 6)]
    [InlineData(1, 1, 0)]
    public void Available_IsOnHandMinusReserved(int onHand, int reserved, int expected)
    {
        // Act
        var available = StockOperations.Available(Stock(onHand, reserved));

        // Assert
        Assert.Equal(expected, available);
    }

    [Theory]
    [InlineData(1, 0, 1, true)]   // exactly the last unit
    [InlineData(1, 1, 1, false)]  // last unit already reserved
    [InlineData(10, 8, 3, false)] // only 2 free
    [InlineData(10, 0, 0, false)] // non-positive request
    public void CanReserve_ReflectsWhetherTheQuantityFits(int onHand, int reserved, int want, bool expected)
    {
        // Act
        var canReserve = StockOperations.CanReserve(Stock(onHand, reserved), want);

        // Assert
        Assert.Equal(expected, canReserve);
    }

    [Fact]
    public void CanReserve_WithNoStockRow_IsFalse()
    {
        // Act
        var canReserve = StockOperations.CanReserve(null, 1);

        // Assert
        Assert.False(canReserve);
    }

    [Fact]
    public void Reserve_ForTheLastUnit_HoldsItAndLeavesZeroAvailable()
    {
        // Arrange
        var stock = Stock(onHand: 1, reserved: 0);

        // Act
        StockOperations.Reserve(stock, 1);

        // Assert
        Assert.Equal(1, stock.QuantityReserved);
        Assert.Equal(1, stock.QuantityOnHand);
        Assert.Equal(0, StockOperations.Available(stock));
    }

    [Fact]
    public void Reserve_WhenItWouldOverbook_ThrowsAndLeavesTheRowUnchanged()
    {
        // Arrange
        var stock = Stock(onHand: 1, reserved: 1);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => StockOperations.Reserve(stock, 1));
        Assert.Equal(1, stock.QuantityReserved);
    }

    [Fact]
    public void Release_GivesReservedUnitsBackWithoutTouchingOnHand()
    {
        // Arrange
        var stock = Stock(onHand: 10, reserved: 2);

        // Act
        StockOperations.Release(stock, 2);

        // Assert
        Assert.Equal(0, stock.QuantityReserved);
        Assert.Equal(10, stock.QuantityOnHand);
    }

    [Fact]
    public void Release_WhenReleasingMoreThanReserved_Throws()
    {
        // Arrange
        var stock = Stock(onHand: 10, reserved: 1);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => StockOperations.Release(stock, 2));
    }

    [Fact]
    public void Consume_PermanentlyRemovesUnitsFromBothOnHandAndReserved()
    {
        // Arrange
        var stock = Stock(onHand: 10, reserved: 3);

        // Act
        StockOperations.Consume(stock, 3);

        // Assert
        Assert.Equal(7, stock.QuantityOnHand);
        Assert.Equal(0, stock.QuantityReserved);
    }

    [Fact]
    public void Consume_WhenReservedIsShort_Throws()
    {
        // Arrange
        var stock = Stock(onHand: 10, reserved: 1);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => StockOperations.Consume(stock, 2));
    }

    [Fact]
    public void ReserveThenRelease_RestoresTheExactStartingQuantities()
    {
        // Arrange
        var stock = Stock(onHand: 5, reserved: 0);

        // Act
        StockOperations.Reserve(stock, 2);
        StockOperations.Release(stock, 2);

        // Assert
        Assert.Equal(5, stock.QuantityOnHand);
        Assert.Equal(0, stock.QuantityReserved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Reserve_WithANonPositiveQuantity_Throws(int quantity)
    {
        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => StockOperations.Reserve(Stock(10, 0), quantity));
    }
}
