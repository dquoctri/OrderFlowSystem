using OrderFlow.Contracts;
using OrderFlow.Messaging;
using OrderFlow.Payments.Infrastructure.Persistence.Entities;

namespace OrderFlow.Payments;

/// <summary>What a charge attempt turns into: a payment row status plus the event to publish.</summary>
public sealed record PaymentRecord(PaymentStatus Status, string EventType, string Topic, object Payload)
{
    public static PaymentRecord From(Guid paymentId, decimal amount, PaymentChargeResult charge) =>
        charge.Approved
            ? new PaymentRecord(
                PaymentStatus.Succeeded,
                nameof(PaymentSucceeded),
                PulsarTopics.PaymentSucceeded,
                new PaymentSucceeded(paymentId, amount))
            : new PaymentRecord(
                PaymentStatus.Failed,
                nameof(PaymentFailed),
                PulsarTopics.PaymentFailed,
                new PaymentFailed(charge.DeclineReason ?? "Payment was declined."));
}
