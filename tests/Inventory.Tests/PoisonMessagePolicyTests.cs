using OrderFlow.Messaging.Resilience;

namespace Inventory.Tests;

public sealed class PoisonMessagePolicyTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    public void ShouldDeadLetter_BeforeTheThirdAttempt_IsFalse(uint redeliveryCount)
    {
        // Act
        var deadLetter = PoisonMessagePolicy.ShouldDeadLetter(redeliveryCount);

        // Assert
        Assert.False(deadLetter);
    }

    [Theory]
    [InlineData(3u)]
    [InlineData(4u)]
    [InlineData(50u)]
    public void ShouldDeadLetter_OnceRedeliveriesReachTheLimit_IsTrue(uint redeliveryCount)
    {
        // Act
        var deadLetter = PoisonMessagePolicy.ShouldDeadLetter(redeliveryCount);

        // Assert
        Assert.True(deadLetter);
    }

    [Fact]
    public void MaxDeliveryAttempts_IsThree_AsTheChallengeRequires()
    {
        // Assert
        Assert.Equal(3u, PoisonMessagePolicy.MaxDeliveryAttempts);
    }
}
