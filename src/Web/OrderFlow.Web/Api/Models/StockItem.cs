namespace OrderFlow.Web.Api.Models;

public sealed record StockItem(string Sku, int QuantityOnHand, int QuantityReserved, int Available);
