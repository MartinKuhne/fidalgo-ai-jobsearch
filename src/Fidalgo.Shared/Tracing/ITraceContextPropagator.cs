using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.Tracing;

/// <summary>
/// Contract for propagating trace context to HTTP requests and extracting from headers.
/// Adheres to the W3C Trace Context standard for trace identifier headers.
/// </summary>
public interface ITraceContextPropagator
{
    /// <summary>Propagates trace context to an HTTP request via W3C traceparent header.</summary>
    void PropagateToHttpRequest(HttpRequestMessage request, TraceContext traceContext);

    /// <summary>Extracts trace context from an incoming HTTP request (stub: returns null).</summary>
    TraceContext? ExtractFromHttpRequest(object request);

    /// <summary>Creates a trace context from HTTP headers.</summary>
    TraceContext CreateFromHeaders(string? traceId, string? spanId, string? correlationId);
}