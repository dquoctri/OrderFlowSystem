namespace OrderFlow.Web.Api.Models;

public sealed record CreateOrderLineRequest(string Sku, int Quantity, decimal UnitPrice);
