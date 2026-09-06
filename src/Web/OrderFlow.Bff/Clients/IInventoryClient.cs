namespace OrderFlow.Bff.Clients;

/// <summary>Typed access to the Inventory service.</summary>
public interface IInventoryClient
{
    Task<IReadOnlyList<UpstreamStockItemDto>> GetStockAsync(CancellationToken cancellationToken);
}
