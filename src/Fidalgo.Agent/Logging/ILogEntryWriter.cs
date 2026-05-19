using Fidalgo.Agent.Models;
using LogLevel = Fidalgo.Agent.Models.LogLevel;

namespace Fidalgo.Agent.Logging;

public interface ILogEntryWriter
{
    Task WriteAsync(LogEntry logEntry, CancellationToken cancellationToken = default);
    Task WriteAsync(
        LogLevel level,
        string message,
        string traceId,
        string spanId,
        string correlationId,
        ExceptionDetails? exception = null,
        IDictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default);
    Task WriteErrorAsync(
        Exception exception,
        string message,
        string traceId,
        string spanId,
        string correlationId,
        IDictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default);
}
