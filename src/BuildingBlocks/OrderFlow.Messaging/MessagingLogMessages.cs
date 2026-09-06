using Microsoft.Extensions.Logging;

namespace OrderFlow.Messaging;

internal static partial class MessagingLogMessages
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Published {EventType} to {Topic} for order {OrderId}.")]
    public static partial void MessagePublished(ILogger logger, string eventType, string topic, Guid orderId);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Handled {EventType} for order {OrderId}.")]
    public static partial void MessageHandled(ILogger logger, string eventType, Guid orderId);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error, Message = "Failed to process {Topic} message; redelivery count is {RedeliveryCount}.")]
    public static partial void MessageProcessingFailed(ILogger logger, Exception exception, string topic, uint redeliveryCount);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "Moved failed message to {DeadLetterTopic}.")]
    public static partial void MessageMovedToDeadLetter(ILogger logger, string deadLetterTopic);
}
