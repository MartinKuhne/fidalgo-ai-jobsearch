using Fidalgo.Shared.Models;
using LogLevel = Fidalgo.Shared.Models.LogLevel;

namespace Fidalgo.Shared.Logging;

/// <summary>
/// Contract for writing structured log entries with trace and correlation context.
/// Bridges internal log models to Microsoft.Extensions.Logging infrastructure.
/// </summary>
public interface ILogEntryWriter
{
    /// <summary>Writes a structured log entry to the logging infrastructure.</summary>
    Task WriteAsync(LogEntry logEntry, CancellationToken cancellationToken = default);

    /// <summary>Writes a log entry with individual parameters.</summary>
    Task WriteAsync(
        LogLevel level,
        string message,
        string traceId,
        string spanId,
        string correlationId,
        ExceptionDetails? exception = null,
        IDictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default);

    /// <summary>Writes an error log entry with exception details.</summary>
    Task WriteErrorAsync(
        Exception exception,
        string message,
        string traceId,
        string spanId,
        string correlationId,
        IDictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default);
}