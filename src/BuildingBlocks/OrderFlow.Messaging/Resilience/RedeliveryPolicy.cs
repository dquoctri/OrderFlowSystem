namespace OrderFlow.Messaging.Resilience;

public static class RedeliveryPolicy
{
    public static readonly TimeSpan[] Delays =
    {
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10)
    };

    public static TimeSpan GetDelay(uint redeliveryCount) =>
        Delays[Math.Min(redeliveryCount, (uint)Delays.Length - 1)];
}
