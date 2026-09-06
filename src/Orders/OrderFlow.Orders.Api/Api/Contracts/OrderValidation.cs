namespace OrderFlow.Orders.Api.Contracts;

/// <summary>Request-level rules for <c>POST /orders</c>: what a valid order looks like, and its total.</summary>
public static class OrderValidation
{
    /// <summary>Returns the rejection message, or <c>null</c> when the request is acceptable.</summary>
    public static string? Validate(CreateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId) || request.Lines is not { Count: > 0 })
        {
            return "customerId and at least one line are required";
        }

        if (request.Lines.Any(line =>
                string.IsNullOrWhiteSpace(line.Sku) || line.Quantity <= 0 || line.UnitPrice < 0))
        {
            return "each line must have a SKU, positive quantity, and non-negative unit price";
        }

        return null;
    }

    /// <summary>Order total = Σ (quantity × unit price). Never called on an invalid request.</summary>
    public static decimal CalculateTotal(IEnumerable<CreateOrderLineRequest> lines) =>
        lines.Sum(line => line.Quantity * line.UnitPrice);
}
