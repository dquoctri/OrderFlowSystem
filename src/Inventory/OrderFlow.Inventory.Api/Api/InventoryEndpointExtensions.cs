using Microsoft.EntityFrameworkCore;
using OrderFlow.Inventory.Api.Contracts;
using OrderFlow.Inventory.Infrastructure.Persistence;
using OrderFlow.Inventory.Infrastructure.Persistence.Entities;

namespace OrderFlow.Inventory.Api;

public static class InventoryEndpointExtensions
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/stock", GetStockAsync);
        endpoints.MapPost("/stock/{sku}/adjust", AdjustStockAsync);
        return endpoints;
    }

    private static async Task<IResult> GetStockAsync(InventoryDbContext db, CancellationToken cancellationToken)
    {
        var stock = await db.StockItems.AsNoTracking().OrderBy(item => item.Sku).ToListAsync(cancellationToken);
        return Results.Ok(stock.Select(ToResponse));
    }

    private static async Task<IResult> AdjustStockAsync(string sku, AdjustStockRequest? request, InventoryDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sku) || request is null || request.Quantity < 0)
        {
            return Results.BadRequest(new { error = "sku and a non-negative quantity are required" });
        }

        var item = await db.StockItems.SingleOrDefaultAsync(value => value.Sku == sku, cancellationToken);
        if (item is null)
        {
            item = new StockItemEntity { Sku = sku, QuantityOnHand = request.Quantity };
            db.StockItems.Add(item);
        }
        else if (request.Quantity < item.QuantityReserved)
        {
            return Results.BadRequest(new { error = "quantity cannot be lower than the currently reserved amount" });
        }
        else
        {
            item.QuantityOnHand = request.Quantity;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(item));
    }

    private static object ToResponse(StockItemEntity item) => new
    {
        sku = item.Sku,
        quantityOnHand = item.QuantityOnHand,
        quantityReserved = item.QuantityReserved,
        available = item.QuantityOnHand - item.QuantityReserved
    };
}
