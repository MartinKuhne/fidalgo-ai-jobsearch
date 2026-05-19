namespace Fidalgo.Agent.Models;

public enum SpanStatus
{
    Unset,
    Ok,
    Error
}

public record Span(
    string SpanId,
    string TraceId,
    string OperationName,
    DateTime StartTime,
    DateTime? EndTime,
    SpanStatus Status,
    IDictionary<string, object>? Attributes,
    List<SpanEvent>? Events,
    List<SpanLink>? Links,
    string? ParentSpanId);
