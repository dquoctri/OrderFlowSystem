using OrderFlow.Orders.Infrastructure;
using OrderFlow.Orders.Infrastructure.Persistence.Entities;

namespace Orders.Tests;

/// <summary>
/// The Orders side of the saga: how each event moves the order, and the guards that stop a late
/// or duplicate event from reviving a finished order.
/// </summary>
public sealed class OrderSagaTransitionsTests
{
    private static OrderEntity Order(OrderStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Status = status,
        SagaState = new OrderSagaStateEntity()
    };

    [Fact]
    public void OrderPlaced_FromPending_MovesToReserving()
    {
        // Arrange
        var order = Order(OrderStatus.Pending);

        // Act
        OrderSagaTransitions.OrderPlaced(order);

        // Assert
        Assert.Equal(OrderStatus.Reserving, order.Status);
    }

    [Fact]
    public void ReservationSucceeded_MovesToChargingAndRecordsTheStep()
    {
        // Arrange
        var order = Order(OrderStatus.Reserving);

        // Act
        OrderSagaTransitions.ReservationSucceeded(order);

        // Assert
        Assert.Equal(OrderStatus.Charging, order.Status);
        Assert.True(order.SagaState.ReservationCompleted);
    }

    [Fact]
    public void ReservationFailed_CancelsWithTheReservationStageAndReason()
    {
        // Arrange
        var order = Order(OrderStatus.Reserving);

        // Act
        OrderSagaTransitions.ReservationFailed(order, "Insufficient stock for SKU WIDGET-01.");

        // Assert
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(FailureStage.ReservationRejected, order.FailureStage);
        Assert.Equal("Insufficient stock for SKU WIDGET-01.", order.FailureReason);
        Assert.NotNull(order.CompletedAt);
    }

    [Fact]
    public void PaymentSucceeded_ConfirmsTheOrderAndStampsCompletedAt()
    {
        // Arrange
        var order = Order(OrderStatus.Charging);

        // Act
        OrderSagaTransitions.PaymentSucceeded(order);

        // Assert
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.True(order.SagaState.PaymentCompleted);
        Assert.NotNull(order.CompletedAt);
    }

    [Fact]
    public void PaymentFailed_CancelsWithThePaymentStageAndReason()
    {
        // Arrange
        var order = Order(OrderStatus.Charging);

        // Act
        OrderSagaTransitions.PaymentFailed(order, "totals ending in .99 are rejected");

        // Assert
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(FailureStage.PaymentDeclined, order.FailureStage);
        Assert.Equal("totals ending in .99 are rejected", order.FailureReason);
    }

    [Fact]
    public void PaymentFailed_OnAnAlreadyConfirmedOrder_IsIgnored()
    {
        // Arrange
        var order = Order(OrderStatus.Confirmed);
        var completedAt = order.CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        // Act
        OrderSagaTransitions.PaymentFailed(order, "late duplicate");

        // Assert
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Null(order.FailureStage);
        Assert.Null(order.FailureReason);
        Assert.Equal(completedAt, order.CompletedAt);
    }

    [Fact]
    public void PaymentSucceeded_OnAnAlreadyCancelledOrder_DoesNotConfirmIt()
    {
        // Arrange
        var order = Order(OrderStatus.Cancelled);
        order.FailureStage = FailureStage.ReservationRejected;

        // Act
        OrderSagaTransitions.PaymentSucceeded(order);

        // Assert
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.True(order.SagaState.PaymentCompleted); // the step is still recorded
    }

    [Fact]
    public void ReservationSucceeded_OnAnAlreadyCancelledOrder_DoesNotRevertIt()
    {
        // Arrange
        var order = Order(OrderStatus.Cancelled);

        // Act
        OrderSagaTransitions.ReservationSucceeded(order);

        // Assert
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }
}
