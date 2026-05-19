namespace Fidalgo.Agent.Models;

public record SpanLink(
    string TraceId,
    string SpanId,
    IDictionary<string, object>? Attributes);
