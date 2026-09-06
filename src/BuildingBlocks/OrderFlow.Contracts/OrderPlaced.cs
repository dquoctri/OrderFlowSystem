namespace OrderFlow.Contracts;

public sealed record OrderPlaced(string CustomerId, IReadOnlyList<OrderLineContract> Lines, decimal TotalAmount);
