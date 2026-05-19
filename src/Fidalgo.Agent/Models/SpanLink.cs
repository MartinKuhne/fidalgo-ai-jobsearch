namespace Fidalgo.Agent.Models;

public record SpanLink
{
    public string TraceId { get; init; } = string.Empty;
    public string SpanId { get; init; } = string.Empty;
    public IDictionary<string, object>? Attributes { get; set; }

    public SpanLink() { }

    public SpanLink(string traceId, string spanId, IDictionary<string, object>? attributes)
    {
        TraceId = traceId;
        SpanId = spanId;
        Attributes = attributes;
    }
}
