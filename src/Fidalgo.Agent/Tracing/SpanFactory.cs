using Fidalgo.Agent.Models;

namespace Fidalgo.Agent.Tracing;

public class SpanFactory : ISpanFactory
{
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

    public void Start(Span span)
    {
        span.StartTime = DateTime.UtcNow;
    }

    public void Stop(Span span, SpanStatus status = SpanStatus.Ok)
    {
        span.EndTime = DateTime.UtcNow;
        span.Status = status;
    }

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

    public void AddAttribute(Span span, string key, object value)
    {
        span.Attributes ??= new Dictionary<string, object>();
        span.Attributes[key] = value;
    }
}
