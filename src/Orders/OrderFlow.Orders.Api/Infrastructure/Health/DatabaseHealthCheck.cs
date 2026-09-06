using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;
using OrderFlow.Orders.Infrastructure.Options;

namespace OrderFlow.Orders.Infrastructure.Health;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly DatabaseOptions options;

    public DatabaseHealthCheck(IOptions<DatabaseOptions> options) => this.options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(options.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'orderflow_orders' AND table_name = '__EFMigrationsHistory')", connection);
            var migrated = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
            return migrated ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Orders database migrations have not been applied.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(exception.Message);
        }
    }
}
