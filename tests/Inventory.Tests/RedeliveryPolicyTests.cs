using OrderFlow.Messaging.Resilience;

namespace Inventory.Tests;

public sealed class RedeliveryPolicyTests
{
    [Theory]
    [InlineData(0u, 2)]
    [InlineData(1u, 5)]
    [InlineData(2u, 10)]
    public void GetDelay_ForEachRetry_BacksOffTwoThenFiveThenTenSeconds(uint redeliveryCount, int expectedSeconds)
    {
        // Act
        var delay = RedeliveryPolicy.GetDelay(redeliveryCount);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Theory]
    [InlineData(3u)]
    [InlineData(99u)]
    public void GetDelay_BeyondTheLastStep_StaysCappedAtTenSeconds(uint redeliveryCount)
    {
        // Act
        var delay = RedeliveryPolicy.GetDelay(redeliveryCount);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(10), delay);
    }
}
