namespace OrderFlow.Contracts;

public sealed record ReservationSucceeded(Guid ReservationId, IReadOnlyList<OrderLineContract> Lines, decimal Amount);
