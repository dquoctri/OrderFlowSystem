namespace OrderFlow.Bff.Composition;

/// <summary>
/// The outcome of composing one <em>degradable</em> part of a view: either a value, or a warning
/// explaining why that part is missing. The overall response still succeeds.
/// </summary>
public sealed record CompositionResult<T>(T? Value, string? Warning)
{
    public bool Degraded => Warning is not null;
}

/// <summary>Non-generic factory (keeps the static helpers off the generic type — CA1000).</summary>
public static class CompositionResult
{
    public static CompositionResult<T> Ok<T>(T value) => new(value, null);

    public static CompositionResult<T> Failed<T>(string warning) => new(default, warning);
}

/// <summary>Thrown when a <em>required</em> downstream (one with nothing to fall back to) is
/// unavailable. The endpoint layer turns this into a 503.</summary>
public sealed class UpstreamUnavailableException : Exception
{
    public UpstreamUnavailableException() { }

    public UpstreamUnavailableException(string message) : base(message) { }

    public UpstreamUnavailableException(string message, Exception innerException) : base(message, innerException) { }

    private UpstreamUnavailableException(string service, string message, Exception innerException)
        : base(message, innerException) => Service = service;

    public string? Service { get; }

    public static UpstreamUnavailableException For(string service, Exception innerException) =>
        new(service, $"The {service} service is unavailable.", innerException);
}
