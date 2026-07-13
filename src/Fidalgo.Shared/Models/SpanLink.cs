namespace Fidalgo.Shared.Models;

/// <summary>
/// Link connecting a span to a span in another trace for cross-trace correlation.
/// </summary>
public record SpanLink
{
    /// <summary>Trace identifier of the linked span.</summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>Span identifier of the linked span.</summary>
    public string SpanId { get; init; } = string.Empty;

    /// <summary>Key-value attributes describing the link.</summary>
    public IDictionary<string, object>? Attributes { get; set; }

    /// <summary>Initializes a new instance with empty defaults.</summary>
    public SpanLink() { }

    /// <summary>Initializes a new instance with link properties.</summary>
    public SpanLink(string traceId, string spanId, IDictionary<string, object>? attributes)
    {
        TraceId = traceId;
        SpanId = spanId;
        Attributes = attributes;
    }
}