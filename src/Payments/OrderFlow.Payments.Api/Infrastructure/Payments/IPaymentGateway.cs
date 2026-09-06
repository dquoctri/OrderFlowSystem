namespace OrderFlow.Payments;

/// <summary>The result of attempting to charge a customer.</summary>
public sealed record PaymentChargeResult(bool Approved, string? DeclineReason)
{
    public static PaymentChargeResult Approve() => new(true, null);
    public static PaymentChargeResult Decline(string reason) => new(false, reason);
}

/// <summary>
/// Boundary to the (fake, in this exercise) payment provider. An interface so the Payments
/// service can be tested with a stub and a real provider dropped in later without touching the saga.
/// </summary>
public interface IPaymentGateway
{
    PaymentChargeResult Charge(Guid orderId, decimal amount);
}
