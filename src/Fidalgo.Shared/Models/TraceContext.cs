namespace Fidalgo.Shared.Models;

/// <summary>
/// Immutable trace context carrying all tracing identifiers for a request.
/// Includes trace ID, span ID, parent span ID, correlation ID, timestamp, and root flag.
/// </summary>
public record TraceContext(
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string CorrelationId,
    DateTime Timestamp,
    bool IsRoot);