using OrderFlow.Orders.Api.Contracts;

namespace Orders.Tests;

public sealed class OrderValidationTests
{
    private static CreateOrderRequest Request(string customerId, params CreateOrderLineRequest[] lines) =>
        new(customerId, lines);

    private static CreateOrderLineRequest Line(string sku = "WIDGET-01", int quantity = 1, decimal unitPrice = 10.00m) =>
        new(sku, quantity, unitPrice);

    [Fact]
    public void Validate_WithCustomerAndOneLine_ReturnsNull()
    {
        // Arrange
        var request = Request("cust-1", Line());

        // Act
        var error = OrderValidation.Validate(request);

        // Assert
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithBlankCustomerId_IsRejected(string customerId)
    {
        // Arrange
        var request = Request(customerId, Line());

        // Act
        var error = OrderValidation.Validate(request);

        // Assert
        Assert.Equal("customerId and at least one line are required", error);
    }

    [Fact]
    public void Validate_WithNoLines_IsRejected()
    {
        // Arrange
        var request = Request("cust-1");

        // Act
        var error = OrderValidation.Validate(request);

        // Assert
        Assert.Equal("customerId and at least one line are required", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithNonPositiveQuantity_IsRejected(int quantity)
    {
        // Arrange
        var request = Request("cust-1", Line(quantity: quantity));

        // Act
        var error = OrderValidation.Validate(request);

        // Assert
        Assert.Equal("each line must have a SKU, positive quantity, and non-negative unit price", error);
    }

    [Fact]
    public void Validate_WithNegativeUnitPrice_IsRejected()
    {
        // Arrange
        var request = Request("cust-1", Line(unitPrice: -0.01m));

        // Act
        var error = OrderValidation.Validate(request);

        // Assert
        Assert.Equal("each line must have a SKU, positive quantity, and non-negative unit price", error);
    }

    [Fact]
    public void Validate_WithABlankSkuOnAnyLine_IsRejected()
    {
        // Arrange
        var request = Request("cust-1", Line(), Line(sku: " "));

        // Act
        var error = OrderValidation.Validate(request);

        // Assert
        Assert.Equal("each line must have a SKU, positive quantity, and non-negative unit price", error);
    }

    [Fact]
    public void CalculateTotal_ForOneLine_IsQuantityTimesUnitPrice()
    {
        // Arrange
        var lines = new[] { new CreateOrderLineRequest("WIDGET-01", 2, 10.00m) };

        // Act
        var total = OrderValidation.CalculateTotal(lines);

        // Assert
        Assert.Equal(20.00m, total);
    }

    [Fact]
    public void CalculateTotal_ForSeveralLines_SumsEachLine()
    {
        // Arrange
        var lines = new[]
        {
            new CreateOrderLineRequest("WIDGET-01", 2, 10.00m),
            new CreateOrderLineRequest("WIDGET-02", 1, 5.50m)
        };

        // Act
        var total = OrderValidation.CalculateTotal(lines);

        // Assert
        Assert.Equal(25.50m, total);
    }

    [Fact]
    public void CalculateTotal_WhenTheTotalEndsIn99_TheRequestIsStillValid()
    {
        // Arrange — the ".99 fails" rule belongs to Payments, not to order creation.
        var request = Request("cust-1", new CreateOrderLineRequest("WIDGET-01", 1, 10.99m));

        // Act
        var error = OrderValidation.Validate(request);
        var total = OrderValidation.CalculateTotal(request.Lines);

        // Assert
        Assert.Null(error);
        Assert.Equal(10.99m, total);
    }
}
