namespace Fidalgo.Agent.Models;

public enum SpanStatus
{
    Unset,
    Ok,
    Error
}

public record Span
{
    public string SpanId { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public string OperationName { get; init; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public SpanStatus Status { get; set; }
    public IDictionary<string, object>? Attributes { get; set; }
    public List<SpanEvent>? Events { get; set; }
    public List<SpanLink>? Links { get; set; }
    public string? ParentSpanId { get; set; }

    public Span() { }

    public Span(
        string spanId,
        string traceId,
        string operationName,
        DateTime startTime,
        DateTime? endTime,
        SpanStatus status,
        IDictionary<string, object>? attributes,
        List<SpanEvent>? events,
        List<SpanLink>? links,
        string? parentSpanId)
    {
        SpanId = spanId;
        TraceId = traceId;
        OperationName = operationName;
        StartTime = startTime;
        EndTime = endTime;
        Status = status;
        Attributes = attributes;
        Events = events;
        Links = links;
        ParentSpanId = parentSpanId;
    }
}
