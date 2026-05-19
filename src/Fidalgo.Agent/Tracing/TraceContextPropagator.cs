using Fidalgo.Agent.Models;

namespace Fidalgo.Agent.Tracing;

public class TraceContextPropagator : ITraceContextPropagator
{
    public void PropagateToHttpRequest(HttpRequestMessage request, TraceContext traceContext)
    {
        request.Headers.Add("traceparent", $"00-{traceContext.TraceId}-{traceContext.SpanId}-01");
        request.Headers.Add("tracestate", $"fidalgo={traceContext.CorrelationId}");
    }

    public TraceContext? ExtractFromHttpRequest(object request)
    {
        return null;
    }

    public TraceContext CreateFromHeaders(string? traceId, string? spanId, string? correlationId)
    {
        var finalTraceId = traceId ?? Guid.NewGuid().ToString("N");
        var finalSpanId = spanId ?? Guid.NewGuid().ToString("N")[..16];
        var finalCorrelationId = correlationId ?? Guid.NewGuid().ToString();

        return new TraceContext(
            TraceId: finalTraceId,
            SpanId: finalSpanId,
            ParentSpanId: null,
            CorrelationId: finalCorrelationId,
            Timestamp: DateTime.UtcNow,
            IsRoot: true);
    }
}
