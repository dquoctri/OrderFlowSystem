namespace OrderFlow.Web.Api.Models;

/// <summary>The composed dashboard payload — one BFF call for both panels.</summary>
/// <param name="Stock"><c>null</c> when the BFF could not reach the Inventory service; see <paramref name="Warnings"/>.</param>
public sealed record DashboardView(
    IReadOnlyList<StockItem>? Stock,
    IReadOnlyList<OrderSummary> Orders,
    IReadOnlyList<string> Warnings,
    DateTimeOffset AsOf);
