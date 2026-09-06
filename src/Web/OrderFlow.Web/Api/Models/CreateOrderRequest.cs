namespace OrderFlow.Web.Api.Models;

public sealed record CreateOrderRequest(string CustomerId, IReadOnlyList<CreateOrderLineRequest> Lines);
