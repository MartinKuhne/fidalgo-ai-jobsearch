namespace Fidalgo.Agent.Models;

public record SpanEvent(
    DateTime Timestamp,
    string Name,
    IDictionary<string, object>? Attributes);
