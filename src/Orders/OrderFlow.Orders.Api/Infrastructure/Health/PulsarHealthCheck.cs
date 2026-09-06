using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OrderFlow.Messaging;

namespace OrderFlow.Orders.Infrastructure.Health;

public sealed class PulsarHealthCheck : IHealthCheck
{
    private readonly PulsarOptions options;
    private readonly IHttpClientFactory httpClientFactory;

    public PulsarHealthCheck(IOptions<PulsarOptions> options, IHttpClientFactory httpClientFactory)
    {
        this.options = options.Value;
        this.httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var adminUrl = options.AdminUrl;
        if (string.IsNullOrWhiteSpace(adminUrl))
        {
            return HealthCheckResult.Unhealthy("Pulsar admin URL is not configured.");
        }

        try
        {
            using var response = await httpClientFactory.CreateClient().GetAsync($"{adminUrl.TrimEnd('/')}/admin/v2/brokers/health", cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Pulsar returned {(int)response.StatusCode}.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(exception.Message);
        }
    }
}
