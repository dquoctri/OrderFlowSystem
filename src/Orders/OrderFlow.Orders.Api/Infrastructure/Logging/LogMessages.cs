using Microsoft.Extensions.Logging;

namespace OrderFlow.Orders.Infrastructure;

internal static partial class LogMessages
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "Orders outbox publishing failed; retrying.")]
    public static partial void OutboxPublishingFailed(ILogger logger, Exception exception);
}
