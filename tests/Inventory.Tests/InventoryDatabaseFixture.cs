using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using OrderFlow.Inventory.Infrastructure.Persistence;

namespace Inventory.Tests;

/// <summary>
/// Spins up a real PostgreSQL 16 in a container for the tests that verify database behaviour
/// (row locking, <c>ON CONFLICT</c> dedup). If Docker is not reachable the fixture does not throw —
/// it records <see cref="SkipReason"/> and the tests skip instead of failing red.
/// </summary>
public sealed class InventoryDatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer? postgres;

    public bool IsAvailable { get; private set; }

    /// <summary>Non-null when Docker could not be used; pass it to <c>Skip.IfNot</c>.</summary>
    public string SkipReason { get; private set; } =
        "Docker is not available; start Docker Desktop to run the Inventory database tests.";

    public DbContextOptions<InventoryDbContext> Options { get; private set; } = null!;
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (!DockerEndpointLooksReachable())
        {
            SkipReason = "Docker does not appear to be running (no docker socket / pipe). " +
                         "Start Docker Desktop to run the Inventory database tests.";
            return;
        }

        try
        {
            postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
            using var startTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            await postgres.StartAsync(startTimeout.Token);
        }
        catch (Exception exception)
        {
            SkipReason = $"Docker is not available for Testcontainers ({exception.GetType().Name}: " +
                         $"{exception.Message.Split('\n')[0]}). Start Docker Desktop to run the Inventory database tests.";
            postgres = null;
            return;
        }

        ConnectionString = postgres.GetConnectionString();
        Options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "orderflow_inventory"))
            .Options;
        await ResetAsync();
        IsAvailable = true;
    }

    public async Task ResetAsync()
    {
        await using var db = new InventoryDbContext(Options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (postgres is not null)
        {
            await postgres.DisposeAsync();
        }
    }

    /// <summary>
    /// Cheap pre-check so a missing Docker skips in milliseconds instead of a ~10s connect timeout.
    /// Note: running <c>docker compose</c> only inside WSL does NOT expose Docker to a Windows
    /// process — Testcontainers needs Docker Desktop (a named pipe) or a <c>DOCKER_HOST</c>.
    /// </summary>
    private static bool DockerEndpointLooksReachable()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            return true; // custom endpoint — let Testcontainers try and report.
        }

        if (!OperatingSystem.IsWindows())
        {
            return File.Exists("/var/run/docker.sock");
        }

        return File.Exists(@"\\.\pipe\docker_engine")               // Docker Desktop (default)
            || File.Exists(@"\\.\pipe\dockerDesktopLinuxEngine");   // Docker Desktop (Linux engine)
    }
}
