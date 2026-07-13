using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.Tracing;

/// <summary>
/// Contract for creating and managing tracing spans within a trace hierarchy.
/// </summary>
public interface ISpanFactory
{
    /// <summary>Creates a new root span for a trace.</summary>
    Span CreateRootSpan(string operationName, TraceContext traceContext);

    /// <summary>Creates a new child span linked to a parent span.</summary>
    Span CreateChildSpan(string operationName, Span parentSpan);

    /// <summary>Starts a span by setting its start time.</summary>
    void Start(Span span);

    /// <summary>Stops a span by setting its end time and status.</summary>
    void Stop(Span span, SpanStatus status = SpanStatus.Ok);

    /// <summary>Adds an event to a span.</summary>
    void AddEvent(Span span, string eventName, IDictionary<string, object>? attributes = null);

    /// <summary>Adds an attribute to a span.</summary>
    void AddAttribute(Span span, string key, object value);
}