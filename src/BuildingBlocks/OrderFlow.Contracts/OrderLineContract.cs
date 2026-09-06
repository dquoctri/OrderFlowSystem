namespace OrderFlow.Contracts;

public sealed record OrderLineContract(string Sku, int Quantity, decimal UnitPrice);
