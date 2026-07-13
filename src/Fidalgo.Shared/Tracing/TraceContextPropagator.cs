using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.Tracing;

/// <summary>
/// Propagates trace context via W3C traceparent header and custom tracestate.
/// Extracts trace context from HTTP requests (stub: returns null).
/// Creates new trace context from header values for incoming requests.
/// </summary>
public class TraceContextPropagator : ITraceContextPropagator
{
    /// <summary>Propagates trace context to an HTTP request via W3C traceparent header.</summary>
    public void PropagateToHttpRequest(HttpRequestMessage request, TraceContext traceContext)
    {
        request.Headers.Add("traceparent", $"00-{traceContext.TraceId}-{traceContext.SpanId}-01");
        request.Headers.Add("tracestate", $"fidalgo={traceContext.CorrelationId}");
    }

    /// <summary>Extracts trace context from an incoming HTTP request (stub: returns null).</summary>
    public TraceContext? ExtractFromHttpRequest(object request)
    {
        return null;
    }

    /// <summary>Creates a trace context from HTTP headers.</summary>
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