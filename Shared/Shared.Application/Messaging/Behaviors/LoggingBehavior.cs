using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Results;

namespace Shared.Application.Messaging.Behaviors;

/// <summary>
/// Logs every command and query with how long it took, and the error code of a failed <see cref="Result"/>.
/// Exceptions pass through untouched; the host's exception handler logs them.
/// </summary>
public sealed class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        var response = await next(message, cancellationToken);

        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        if (response is Result { Success: false } failed)
        {
            logger.LogInformation("{Message} failed with {ErrorCode} in {Elapsed:0.0} ms",
                typeof(TMessage).Name, failed.ErrorCode, elapsed);
        }
        else
        {
            logger.LogDebug("{Message} handled in {Elapsed:0.0} ms", typeof(TMessage).Name, elapsed);
        }

        return response;
    }
}
