namespace Fidalgo.Agent.Models;

public record TraceContext(
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string CorrelationId,
    DateTime Timestamp,
    bool IsRoot);
