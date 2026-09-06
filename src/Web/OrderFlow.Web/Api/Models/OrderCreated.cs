namespace OrderFlow.Web.Api.Models;

public sealed record OrderCreated(Guid OrderId, Guid CorrelationId, string Status);
