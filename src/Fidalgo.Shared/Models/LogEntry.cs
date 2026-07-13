namespace Fidalgo.Shared.Models;

/// <summary>
/// Structured log levels mirroring Microsoft.Extensions.Logging levels.
/// Used for logging infrastructure independent of any specific logging provider.
/// </summary>
public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Immutable structured log entry carrying trace context and exception details.
/// Serves as the common data model for all logging operations before mapping to providers.
/// </summary>
public record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Message,
    string TraceId,
    string SpanId,
    string CorrelationId,
    ExceptionDetails? Exception,
    IReadOnlyDictionary<string, object>? Properties);