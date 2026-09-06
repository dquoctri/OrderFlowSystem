namespace OrderFlow.Payments;

/// <summary>
/// Deterministic stand-in for a real provider: it declines any total whose cents are exactly
/// <c>.99</c> (e.g. <c>19.99</c>), so the failure path is easy and repeatable to demo.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    public const string DeclineReason = "The fake payment gateway rejects totals ending in .99.";

    /// <summary>The raw rule, exposed for direct assertions.</summary>
    public static bool WouldDecline(decimal amount) => decimal.Round(amount % 1m, 2) == 0.99m;

    public PaymentChargeResult Charge(Guid orderId, decimal amount) =>
        WouldDecline(amount)
            ? PaymentChargeResult.Decline(DeclineReason)
            : PaymentChargeResult.Approve();
}
