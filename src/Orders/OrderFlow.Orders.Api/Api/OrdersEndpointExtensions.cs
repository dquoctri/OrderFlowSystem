using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Contracts;
using OrderFlow.Orders.Api.Contracts;
using OrderFlow.Orders.Infrastructure.Persistence;
using OrderFlow.Orders.Infrastructure.Persistence.Entities;
using OrderFlow.Messaging;

namespace OrderFlow.Orders.Api;

public static class OrdersEndpointExtensions
{
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/orders", CreateOrderAsync);
        endpoints.MapGet("/orders/{id:guid}", GetOrderAsync);
        endpoints.MapGet("/orders/{id:guid}/trace", GetOrderTraceAsync);
        endpoints.MapGet("/orders", GetOrdersAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateOrderAsync(CreateOrderRequest request, OrdersDbContext db, CancellationToken cancellationToken)
    {
        if (OrderValidation.Validate(request) is { } error)
        {
            return Results.BadRequest(new { error });
        }

        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var total = OrderValidation.CalculateTotal(request.Lines);
        var order = new OrderEntity
        {
            Id = orderId,
            CustomerId = request.CustomerId,
            TotalAmount = total,
            Status = OrderStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            SagaState = new OrderSagaStateEntity { OrderId = orderId },
            Lines = request.Lines.Select(line => new OrderLineEntity
            {
                Sku = line.Sku,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice
            }).ToList()
        };

        var eventId = Guid.NewGuid();
        var envelope = new EventEnvelope<OrderPlaced>(
            eventId,
            orderId,
            orderId,
            now,
            new OrderPlaced(
                request.CustomerId,
                request.Lines.Select(line => new OrderLineContract(line.Sku, line.Quantity, line.UnitPrice)).ToList(),
                total));

        db.Orders.Add(order);
        db.OutboxMessages.Add(new OutboxMessageEntity
        {
            EventId = eventId,
            OrderId = orderId,
            CorrelationId = envelope.CorrelationId,
            EventType = nameof(OrderPlaced),
            Topic = PulsarTopics.OrderPlaced,
            Payload = JsonSerializer.Serialize(envelope),
            CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Accepted($"/orders/{orderId}", new { orderId, correlationId = orderId, status = order.Status.ToString() });
    }

    private static async Task<IResult> GetOrderAsync(Guid id, OrdersDbContext db, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(value => value.Lines)
            .Include(value => value.SagaState)
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

        return order is null ? Results.NotFound() : Results.Ok(ToResponse(order));
    }

    private static async Task<IResult> GetOrderTraceAsync(Guid id, OrdersDbContext db, CancellationToken cancellationToken)
    {
        var exists = await db.Orders.AsNoTracking().AnyAsync(order => order.Id == id, cancellationToken);
        if (!exists)
        {
            return Results.NotFound();
        }

        var log = await db.OrderSagaLog.AsNoTracking()
            .Where(entry => entry.OrderId == id)
            .OrderBy(entry => entry.Seq)
            .ToListAsync(cancellationToken);

        return Results.Ok(log.Select(OrderFlow.Orders.Infrastructure.Streaming.SagaRow.FromEntity));
    }

    private static async Task<IResult> GetOrdersAsync(string? customerId, OrdersDbContext db, CancellationToken cancellationToken)
    {
        var query = db.Orders.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(customerId))
        {
            query = query.Where(order => order.CustomerId == customerId);
        }

        var orders = await query.OrderByDescending(order => order.CreatedAt)
            .Select(order => new { orderId = order.Id, order.Status, order.TotalAmount, order.CreatedAt })
            .ToListAsync(cancellationToken);
        return Results.Ok(orders.Select(order => new
        {
            order.orderId,
            status = order.Status.ToString(),
            order.TotalAmount,
            order.CreatedAt
        }));
    }

    private static object ToResponse(OrderEntity order) => new
    {
        orderId = order.Id,
        customerId = order.CustomerId,
        status = order.Status.ToString(),
        totalAmount = order.TotalAmount,
        reservationCompleted = order.SagaState.ReservationCompleted,
        paymentCompleted = order.SagaState.PaymentCompleted,
        failureStage = order.FailureStage?.ToString(),
        failureReason = order.FailureReason,
        completedAt = order.CompletedAt,
        lines = order.Lines.Select(line => new { sku = line.Sku, quantity = line.Quantity, unitPrice = line.UnitPrice }),
        order.CreatedAt,
        order.UpdatedAt
    };
}
