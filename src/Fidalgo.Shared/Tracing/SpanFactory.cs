using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.Tracing;

/// <summary>
/// Factory for creating and managing tracing spans with unique IDs and timing data.
/// Creates root and child spans, starts/stops them, and adds events and attributes.
/// Mutates span objects in-place for state management.
/// </summary>
public class SpanFactory : ISpanFactory
{
    /// <summary>Creates a new root span for a trace.</summary>
    public Span CreateRootSpan(string operationName, TraceContext traceContext)
    {
        return new Span
        {
            SpanId = Guid.NewGuid().ToString("N")[..16],
            TraceId = traceContext.TraceId,
            OperationName = operationName,
            StartTime = DateTime.UtcNow,
            Status = SpanStatus.Unset,
            Attributes = new Dictionary<string, object>(),
            Events = new List<SpanEvent>(),
            Links = new List<SpanLink>(),
            ParentSpanId = null
        };
    }

    /// <summary>Creates a new child span linked to a parent span.</summary>
    public Span CreateChildSpan(string operationName, Span parentSpan)
    {
        return new Span
        {
            SpanId = Guid.NewGuid().ToString("N")[..16],
            TraceId = parentSpan.TraceId,
            OperationName = operationName,
            StartTime = DateTime.UtcNow,
            Status = SpanStatus.Unset,
            Attributes = new Dictionary<string, object>(),
            Events = new List<SpanEvent>(),
            Links = new List<SpanLink>(),
            ParentSpanId = parentSpan.SpanId
        };
    }

    /// <summary>Starts a span by setting its start time.</summary>
    public void Start(Span span)
    {
        span.StartTime = DateTime.UtcNow;
    }

    /// <summary>Stops a span by setting its end time and status.</summary>
    public void Stop(Span span, SpanStatus status = SpanStatus.Ok)
    {
        span.EndTime = DateTime.UtcNow;
        span.Status = status;
    }

    /// <summary>Adds an event to a span.</summary>
    public void AddEvent(Span span, string eventName, IDictionary<string, object>? attributes = null)
    {
        span.Events ??= new List<SpanEvent>();
        span.Events.Add(new SpanEvent
        {
            Timestamp = DateTime.UtcNow,
            Name = eventName,
            Attributes = attributes
        });
    }

    /// <summary>Adds an attribute to a span.</summary>
    public void AddAttribute(Span span, string key, object value)
    {
        span.Attributes ??= new Dictionary<string, object>();
        span.Attributes[key] = value;
    }
}