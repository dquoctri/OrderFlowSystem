namespace OrderFlow.Contracts;

public sealed record PaymentSucceeded(Guid PaymentId, decimal Amount);
