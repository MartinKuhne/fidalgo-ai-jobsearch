namespace Fidalgo.Shared.Models;

/// <summary>
/// Event that occurred during a span's lifetime with a timestamp and attributes.
/// </summary>
public record SpanEvent
{
    /// <summary>Timestamp when the event occurred.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Human-readable name of the event.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Key-value attributes describing the event.</summary>
    public IDictionary<string, object>? Attributes { get; set; }

    /// <summary>Initializes a new instance with empty defaults.</summary>
    public SpanEvent() { }

    /// <summary>Initializes a new instance with event properties.</summary>
    public SpanEvent(DateTime timestamp, string name, IDictionary<string, object>? attributes)
    {
        Timestamp = timestamp;
        Name = name;
        Attributes = attributes;
    }
}