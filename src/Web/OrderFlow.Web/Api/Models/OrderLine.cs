namespace OrderFlow.Web.Api.Models;

public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice);
