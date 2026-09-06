namespace OrderFlow.Contracts;

public sealed record StockReleased(IReadOnlyList<OrderLineContract> ReleasedLines);
