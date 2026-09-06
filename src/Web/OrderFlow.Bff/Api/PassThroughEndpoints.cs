using System.Text.Json;
using OrderFlow.Bff.Clients;

namespace OrderFlow.Bff.Api;

/// <summary>
/// Thin relays to the Orders service so the SPA only ever talks to one origin (the BFF).
/// No reshaping — status code and JSON body are forwarded verbatim.
/// </summary>
public static class PassThroughEndpoints
{
    public static IEndpointRouteBuilder MapPassThroughEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/orders", CreateOrderAsync);
        endpoints.MapGet("/orders/{id:guid}", (Guid id, IOrdersClient orders, CancellationToken ct) =>
            RelayAsync(orders, $"/orders/{id:D}", ct));
        endpoints.MapGet("/orders/{id:guid}/trace", (Guid id, IOrdersClient orders, CancellationToken ct) =>
            RelayAsync(orders, $"/orders/{id:D}/trace", ct));
        return endpoints;
    }

    private static async Task<IResult> CreateOrderAsync(JsonElement body, IOrdersClient orders, CancellationToken cancellationToken)
    {
        var result = await orders.CreateOrderAsync(body, cancellationToken);
        return Relay(result);
    }

    private static async Task<IResult> RelayAsync(IOrdersClient orders, string relativePath, CancellationToken cancellationToken)
    {
        var result = await orders.GetRawAsync(relativePath, cancellationToken);
        return Relay(result);
    }

    private static IResult Relay(UpstreamResult result) => result.Json is null
        ? Results.StatusCode(result.StatusCode)
        : Results.Content(result.Json, "application/json", null, result.StatusCode);
}
