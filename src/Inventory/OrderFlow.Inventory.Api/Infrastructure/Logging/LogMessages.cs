using Microsoft.Extensions.Logging;

namespace OrderFlow.Inventory.Infrastructure;

internal static partial class LogMessages
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "Inventory outbox publishing failed; retrying.")]
    public static partial void OutboxPublishingFailed(ILogger logger, Exception exception);
}
