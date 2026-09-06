using Microsoft.Extensions.Logging;

namespace OrderFlow.Payments.Infrastructure;

internal static partial class LogMessages
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "Payments outbox publishing failed; retrying.")]
    public static partial void OutboxPublishingFailed(ILogger logger, Exception exception);
}
