using System.ComponentModel.DataAnnotations;

namespace OrderFlow.Messaging;

public sealed class PulsarOptions
{
    [Required]
    public string ServiceUrl { get; set; } = string.Empty;

    [Required]
    public string AdminUrl { get; set; } = string.Empty;
}
