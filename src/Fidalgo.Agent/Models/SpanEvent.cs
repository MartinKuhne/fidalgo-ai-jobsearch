namespace Fidalgo.Agent.Models;

public record SpanEvent
{
    public DateTime Timestamp { get; set; }
    public string Name { get; init; } = string.Empty;
    public IDictionary<string, object>? Attributes { get; set; }

    public SpanEvent() { }

    public SpanEvent(DateTime timestamp, string name, IDictionary<string, object>? attributes)
    {
        Timestamp = timestamp;
        Name = name;
        Attributes = attributes;
    }
}
