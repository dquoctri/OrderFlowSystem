using Microsoft.EntityFrameworkCore;
using OrderFlow.Payments.Infrastructure.Persistence;

namespace OrderFlow.Payments.Api;

public static class PaymentsEndpointExtensions
{
    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/payments/{orderId:guid}", GetPaymentAsync);
        return endpoints;
    }

    private static async Task<IResult> GetPaymentAsync(Guid orderId, PaymentsDbContext db, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(value => value.OrderId == orderId, cancellationToken);
        return payment is null
            ? Results.NotFound()
            : Results.Ok(new
            {
                paymentId = payment.Id,
                payment.OrderId,
                payment.Amount,
                status = payment.Status.ToString(),
                payment.CreatedAt
            });
    }
}
