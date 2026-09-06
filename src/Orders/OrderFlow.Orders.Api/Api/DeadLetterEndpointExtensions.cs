using OrderFlow.Messaging;

namespace OrderFlow.Orders.Api;

public static class DeadLetterEndpointExtensions
{
    public static IEndpointRouteBuilder MapDeadLetterEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/admin/dead-letters", async (string? topic, int? limit, PulsarEventBus eventBus, CancellationToken cancellationToken) =>
        {
            var resolvedTopic = PulsarTopics.Resolve(topic);
            return resolvedTopic is null
                ? Results.BadRequest(new { error = "topic must be one of the known OrderFlow topics" })
                : Results.Ok(await eventBus.ReadDeadLettersAsync(resolvedTopic, limit ?? 20, cancellationToken));
        });
        endpoints.MapPost("/admin/dead-letters/replay", async (string? topic, int? limit, PulsarEventBus eventBus, CancellationToken cancellationToken) =>
        {
            var resolvedTopic = PulsarTopics.Resolve(topic);
            return resolvedTopic is null
                ? Results.BadRequest(new { error = "topic must be one of the known OrderFlow topics" })
                : Results.Ok(new { replayed = await eventBus.ReplayDeadLettersAsync(resolvedTopic, limit ?? 20, cancellationToken) });
        });
        return endpoints;
    }
}
