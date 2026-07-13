namespace Fidalgo.Shared.Models;

/// <summary>
/// Status values for tracing spans indicating operation outcome.
/// </summary>
public enum SpanStatus
{
    Unset,
    Ok,
    Error
}

/// <summary>
/// Tracing span representing a timed operation within a trace.
/// Contains identifiers, timing data, status, attributes, events, links, and parent relationship.
/// Mutable properties required by SpanFactory for in-place updates.
/// </summary>
public record Span
{
    /// <summary>Unique identifier for this span.</summary>
    public string SpanId { get; init; } = string.Empty;

    /// <summary>Trace identifier linking all spans in a trace.</summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>Human-readable name of the operation this span represents.</summary>
    public string OperationName { get; init; } = string.Empty;

    /// <summary>Start time of the operation.</summary>
    public DateTime StartTime { get; set; }

    /// <summary>End time of the operation, or null if not yet stopped.</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>Status indicating success or failure.</summary>
    public SpanStatus Status { get; set; }

    /// <summary>Key-value attributes describing the operation.</summary>
    public IDictionary<string, object>? Attributes { get; set; }

    /// <summary>Events that occurred during the operation.</summary>
    public List<SpanEvent>? Events { get; set; }

    /// <summary>Links to related spans in other traces.</summary>
    public List<SpanLink>? Links { get; set; }

    /// <summary>Parent span ID, or null if this is a root span.</summary>
    public string? ParentSpanId { get; set; }

    /// <summary>Initializes a new instance with empty defaults.</summary>
    public Span() { }

    /// <summary>Initializes a new instance with all span properties.</summary>
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