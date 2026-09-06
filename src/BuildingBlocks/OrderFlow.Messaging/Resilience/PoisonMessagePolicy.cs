namespace OrderFlow.Messaging.Resilience;

/// <summary>
/// When a message has failed enough times it is "poison": stop retrying and move it to the
/// dead-letter topic so it can't block the rest of the pipeline. The challenge asks for a DLQ
/// after three failed delivery attempts.
/// </summary>
public static class PoisonMessagePolicy
{
    /// <summary>Delivery attempts allowed before a message is dead-lettered.</summary>
    public const uint MaxDeliveryAttempts = 3;

    /// <summary>
    /// <paramref name="redeliveryCount"/> is Pulsar's count of prior redeliveries (0 on first
    /// delivery). Once it reaches <see cref="MaxDeliveryAttempts"/> the message is dead-lettered.
    /// </summary>
    public static bool ShouldDeadLetter(uint redeliveryCount) => redeliveryCount >= MaxDeliveryAttempts;
}
