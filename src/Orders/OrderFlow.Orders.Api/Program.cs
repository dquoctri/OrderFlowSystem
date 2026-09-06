using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderFlow.Messaging;
using OrderFlow.Orders.Api;
using OrderFlow.Orders.Infrastructure.Database;
using OrderFlow.Orders.Infrastructure.Health;
using OrderFlow.Orders.Infrastructure.Messaging;
using OrderFlow.Orders.Infrastructure.Options;
using OrderFlow.Orders.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(60));
builder.Services.AddHttpClient();
builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:5000")
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection("Database"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<PulsarOptions>()
    .Bind(builder.Configuration.GetSection("Pulsar"))
    .ValidateDataAnnotations()
    .Validate(options => Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out var uri) && uri.Scheme == "pulsar", "Pulsar:ServiceUrl must be an absolute pulsar:// URI.")
    .Validate(options => Uri.TryCreate(options.AdminUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https", "Pulsar:AdminUrl must be an absolute HTTP(S) URI.")
    .ValidateOnStart();
builder.Services.AddDbContext<OrdersDbContext>((serviceProvider, options) =>
{
    var database = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    options.UseNpgsql(database.ConnectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "orderflow_orders"));
});
builder.Services.AddSingleton(serviceProvider =>
{
    var pulsar = serviceProvider.GetRequiredService<IOptions<PulsarOptions>>().Value;
    return new PulsarEventBus(pulsar.ServiceUrl, serviceProvider.GetRequiredService<ILogger<PulsarEventBus>>());
});
builder.Services.AddHostedService<OrdersOutboxPublisher>();
builder.Services.AddHostedService<OrderPlacedConsumer>();
builder.Services.AddHostedService<ReservationSucceededConsumer>();
builder.Services.AddHostedService<ReservationFailedConsumer>();
builder.Services.AddHostedService<PaymentSucceededConsumer>();
builder.Services.AddHostedService<PaymentFailedConsumer>();
builder.Services.AddHostedService<StockReleasedConsumer>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<PulsarHealthCheck>("pulsar", tags: ["ready"]);

var app = builder.Build();

if (args.Any(argument => string.Equals(argument, "--migrate", StringComparison.OrdinalIgnoreCase)))
{
    _ = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    await DatabaseInitializer.MigrateAsync(app.Services);
    await app.DisposeAsync();
    return;
}

_ = app.Services.GetRequiredService<IOptions<PulsarOptions>>().Value;
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.MapOrdersEndpoints();
app.MapDeadLetterEndpoints();
app.MapGet("/", () => Results.Ok(new { service = "orders" }));
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

public partial class Program { }
