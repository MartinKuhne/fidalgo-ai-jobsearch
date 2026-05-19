using Fidalgo.Agent.Models;

namespace Fidalgo.Agent.Tracing;

public interface ITraceContextPropagator
{
    void PropagateToHttpRequest(HttpRequestMessage request, TraceContext traceContext);
    TraceContext? ExtractFromHttpRequest(object request);
    TraceContext CreateFromHeaders(string? traceId, string? spanId, string? correlationId);
}
