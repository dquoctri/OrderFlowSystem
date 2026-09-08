using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OrderFlow.Web;
using OrderFlow.Web.Api;
using OrderFlow.Web.Api.Models;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
var apiUrls = builder.Configuration.GetSection("ApiUrls").Get<ApiUrls>()
    ?? throw new InvalidOperationException("ApiUrls configuration is required.");
builder.Services.AddSingleton(apiUrls);
builder.Services.AddScoped<SagaStreamClient>();
builder.Services.AddHttpClient<OrderFlowApiClient>(client =>
{
    client.BaseAddress = new Uri(apiUrls.Bff.TrimEnd('/') + "/");
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
});

await builder.Build().RunAsync();
