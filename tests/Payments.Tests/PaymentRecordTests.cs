using OrderFlow.Contracts;
using OrderFlow.Messaging;
using OrderFlow.Payments;
using OrderFlow.Payments.Infrastructure.Persistence.Entities;

namespace Payments.Tests;

public sealed class PaymentRecordTests
{
    private static readonly Guid PaymentId = Guid.NewGuid();

    [Fact]
    public void From_WhenChargeApproved_ProducesASucceededPaymentAndEvent()
    {
        // Arrange
        var charge = PaymentChargeResult.Approve();

        // Act
        var record = PaymentRecord.From(PaymentId, 25.50m, charge);

        // Assert
        Assert.Equal(PaymentStatus.Succeeded, record.Status);
        Assert.Equal(nameof(PaymentSucceeded), record.EventType);
        Assert.Equal(PulsarTopics.PaymentSucceeded, record.Topic);
        var payload = Assert.IsType<PaymentSucceeded>(record.Payload);
        Assert.Equal(PaymentId, payload.PaymentId);
        Assert.Equal(25.50m, payload.Amount);
    }

    [Fact]
    public void From_WhenChargeDeclined_ProducesAFailedPaymentAndCarriesTheReason()
    {
        // Arrange
        var charge = PaymentChargeResult.Decline("insufficient funds");

        // Act
        var record = PaymentRecord.From(PaymentId, 25.50m, charge);

        // Assert
        Assert.Equal(PaymentStatus.Failed, record.Status);
        Assert.Equal(nameof(PaymentFailed), record.EventType);
        Assert.Equal(PulsarTopics.PaymentFailed, record.Topic);
        var payload = Assert.IsType<PaymentFailed>(record.Payload);
        Assert.Equal("insufficient funds", payload.Reason);
    }

    [Fact]
    public void From_WithTheFakeGateway_DeclinesA99Total()
    {
        // Arrange
        IPaymentGateway gateway = new FakePaymentGateway();
        var charge = gateway.Charge(Guid.NewGuid(), 10.99m);

        // Act
        var record = PaymentRecord.From(PaymentId, 10.99m, charge);

        // Assert
        Assert.Equal(PaymentStatus.Failed, record.Status);
        Assert.Equal(FakePaymentGateway.DeclineReason, Assert.IsType<PaymentFailed>(record.Payload).Reason);
    }
}
