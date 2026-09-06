using OrderFlow.Bff.Composition;

namespace OrderFlow.Bff.Api;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/dashboard", GetDashboardAsync);
        endpoints.MapGet("/dashboard/orders/{id:guid}", GetTrackedOrderAsync);
        return endpoints;
    }

    private static async Task<IResult> GetDashboardAsync(DashboardComposer composer, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await composer.ComposeDashboardAsync(cancellationToken));
        }
        catch (UpstreamUnavailableException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> GetTrackedOrderAsync(Guid id, DashboardComposer composer, CancellationToken cancellationToken)
    {
        try
        {
            var view = await composer.ComposeTrackedOrderAsync(id, cancellationToken);
            return view is null ? Results.NotFound() : Results.Ok(view);
        }
        catch (UpstreamUnavailableException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
