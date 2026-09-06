using System.ComponentModel.DataAnnotations;

namespace OrderFlow.Bff.Infrastructure.Options;

/// <summary>
/// Where the BFF finds the services it composes, and how long it is willing to wait.
/// Bound from configuration section <c>Downstream</c> (env vars <c>Downstream__OrdersBaseUrl</c>,
/// <c>Downstream__InventoryBaseUrl</c>, <c>Downstream__RequestTimeoutSeconds</c>).
/// </summary>
public sealed class DownstreamOptions : IValidatableObject
{
    [Required]
    public string OrdersBaseUrl { get; set; } = string.Empty;

    [Required]
    public string InventoryBaseUrl { get; set; } = string.Empty;

    /// <summary>Overall budget for one composed request. The composer cancels every
    /// still-running downstream call when this elapses, so one slow service cannot hang the page.</summary>
    [Range(1, 30)]
    public int RequestTimeoutSeconds { get; set; } = 3;

    /// <summary>How long a pooled connection to a downstream lives before it is dropped and DNS
    /// is re-resolved. Keeps traffic spread across replicas when a service is scaled. See §11.3 of
    /// the plan.</summary>
    [Range(10, 600)]
    public int ConnectionLifetimeSeconds { get; set; } = 120;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (name, value) in new[] { (nameof(OrdersBaseUrl), OrdersBaseUrl), (nameof(InventoryBaseUrl), InventoryBaseUrl) })
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            {
                yield return new ValidationResult($"{name} must be an absolute HTTP(S) URI.", new[] { name });
            }
        }
    }
}
