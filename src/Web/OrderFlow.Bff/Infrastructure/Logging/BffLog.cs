using Microsoft.Extensions.Logging;

namespace OrderFlow.Bff.Infrastructure.Logging;

internal static partial class BffLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Downstream {Service} degraded; the view was returned without it.")]
    public static partial void DownstreamDegraded(ILogger logger, string service, Exception exception);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "Required downstream {Service} is unavailable; responding 503.")]
    public static partial void DownstreamFatal(ILogger logger, string service, Exception exception);
}
