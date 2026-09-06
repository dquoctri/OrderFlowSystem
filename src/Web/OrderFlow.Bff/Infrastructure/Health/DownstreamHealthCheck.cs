using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OrderFlow.Bff.Infrastructure.Options;

namespace OrderFlow.Bff.Infrastructure.Health;

/// <summary>Reports ready only when both composed services answer <c>/health/live</c>.</summary>
public sealed class DownstreamHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly DownstreamOptions options;

    public DownstreamHealthCheck(IHttpClientFactory httpClientFactory, IOptions<DownstreamOptions> options)
    {
        this.httpClientFactory = httpClientFactory;
        this.options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("health");
        var unreachable = new List<string>();

        foreach (var (name, baseUrl) in new[] { ("orders", options.OrdersBaseUrl), ("inventory", options.InventoryBaseUrl) })
        {
            try
            {
                using var response = await client.GetAsync(new Uri($"{baseUrl.TrimEnd('/')}/health/live"), cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    unreachable.Add($"{name} ({(int)response.StatusCode})");
                }
            }
            catch (Exception exception)
            {
                unreachable.Add($"{name} ({exception.GetType().Name})");
            }
        }

        return unreachable.Count == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"Unreachable: {string.Join(", ", unreachable)}");
    }
}
