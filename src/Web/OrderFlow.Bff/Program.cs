using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OrderFlow.Bff.Api;
using OrderFlow.Bff.Clients;
using OrderFlow.Bff.Composition;
using OrderFlow.Bff.Infrastructure.Health;
using OrderFlow.Bff.Infrastructure.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:5000")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddOptions<DownstreamOptions>()
    .Bind(builder.Configuration.GetSection("Downstream"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var downstream = builder.Configuration.GetSection("Downstream").Get<DownstreamOptions>() ?? new DownstreamOptions();
var connectionLifetime = TimeSpan.FromSeconds(Math.Clamp(downstream.ConnectionLifetimeSeconds, 10, 600));

builder.Services.AddHttpClient("health");

// One typed client per downstream.
//  - PooledConnectionLifetime recycles the connection pool so Docker DNS is re-resolved and
//    traffic keeps spreading when a downstream is scaled (plan §11.3).
//  - AddStandardResilienceHandler adds retry + per-attempt timeout + circuit breaker.
builder.Services.AddHttpClient<IOrdersClient, OrdersClient>(client => SetBaseAddress(client, downstream.OrdersBaseUrl))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = connectionLifetime })
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IInventoryClient, InventoryClient>(client => SetBaseAddress(client, downstream.InventoryBaseUrl))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = connectionLifetime })
    .AddStandardResilienceHandler();

builder.Services.AddScoped<DashboardComposer>();

builder.Services.AddHealthChecks()
    .AddCheck<DownstreamHealthCheck>("downstream", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

app.MapDashboardEndpoints();
app.MapPassThroughEndpoints();
app.MapGet("/", () => Results.Ok(new { service = "bff" }));
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

static void SetBaseAddress(HttpClient client, string baseUrl)
{
    if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
    {
        client.BaseAddress = uri;
    }
    // else: DownstreamOptions validation fails ValidateOnStart with a readable message.
}

public partial class Program { }
