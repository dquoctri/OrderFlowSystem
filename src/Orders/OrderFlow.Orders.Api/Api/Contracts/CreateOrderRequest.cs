namespace OrderFlow.Orders.Api.Contracts;

public sealed record CreateOrderRequest(string CustomerId, IReadOnlyList<CreateOrderLineRequest> Lines);
