namespace OrderFlow.Orders.Api.Contracts;

public sealed record CreateOrderLineRequest(string Sku, int Quantity, decimal UnitPrice);
