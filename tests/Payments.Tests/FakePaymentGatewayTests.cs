using System.Globalization;
using OrderFlow.Payments;

namespace Payments.Tests;

public sealed class FakePaymentGatewayTests
{
    private readonly IPaymentGateway gateway = new FakePaymentGateway();

    [Theory]
    [InlineData("19.99")]
    [InlineData("0.99")]
    [InlineData("100.99")]
    [InlineData("1000.99")]
    public void Charge_WhenTotalCentsAreExactly99_IsDeclined(string amount)
    {
        // Arrange
        var total = decimal.Parse(amount, CultureInfo.InvariantCulture);

        // Act
        var result = gateway.Charge(Guid.NewGuid(), total);

        // Assert
        Assert.False(result.Approved);
        Assert.Equal(FakePaymentGateway.DeclineReason, result.DeclineReason);
    }

    [Theory]
    [InlineData("19.98")]
    [InlineData("20.00")]
    [InlineData("19.90")]
    [InlineData("0.01")]
    [InlineData("25.50")]
    [InlineData("100.00")]
    public void Charge_WhenTotalCentsAreAnythingElse_IsApproved(string amount)
    {
        // Arrange
        var total = decimal.Parse(amount, CultureInfo.InvariantCulture);

        // Act
        var result = gateway.Charge(Guid.NewGuid(), total);

        // Assert
        Assert.True(result.Approved);
        Assert.Null(result.DeclineReason);
    }

    [Fact]
    public void WouldDecline_MatchesTheChargeOutcome_ForA99Total()
    {
        // Arrange
        const decimal total = 10.99m;

        // Act
        var ruleSaysDecline = FakePaymentGateway.WouldDecline(total);
        var chargeApproved = gateway.Charge(Guid.NewGuid(), total).Approved;

        // Assert
        Assert.True(ruleSaysDecline);
        Assert.False(chargeApproved);
    }
}
