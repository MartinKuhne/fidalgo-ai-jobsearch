using Fidalgo.Agent.Models;

namespace Fidalgo.Agent.Tracing;

public interface ISpanFactory
{
    Span CreateRootSpan(string operationName, TraceContext traceContext);
    Span CreateChildSpan(string operationName, Span parentSpan);
    void Start(Span span);
    void Stop(Span span, SpanStatus status = SpanStatus.Ok);
    void AddEvent(Span span, string eventName, IDictionary<string, object>? attributes = null);
    void AddAttribute(Span span, string key, object value);
}
