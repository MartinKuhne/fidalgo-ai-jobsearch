namespace Fidalgo.Agent.Models;

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

public record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Message,
    string TraceId,
    string SpanId,
    string CorrelationId,
    ExceptionDetails? Exception,
    IDictionary<string, object>? Properties);
