using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.Logging;

/// <summary>
/// HttpMessageHandler that injects W3C trace context headers into outgoing requests.
/// </summary>
public class TracingHttpMessageHandler : DelegatingHandler
{
    private readonly ITraceContextProvider _traceContextProvider;

    /// <summary>
    /// Initializes a new instance of the TracingHttpMessageHandler class.
    /// </summary>
    /// <param name="traceContextProvider">The trace context provider.</param>
    public TracingHttpMessageHandler(ITraceContextProvider traceContextProvider)
    {
        _traceContextProvider = traceContextProvider;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var traceContext = _traceContextProvider.GetCurrentContext();
        if (traceContext != null && !string.IsNullOrEmpty(traceContext.TraceId))
        {
            // Add W3C traceparent header: 00-traceid-spanid-traceflags
            var traceId = traceContext.TraceId.PadLeft(32, '0');
            var spanId = traceContext.SpanId.PadLeft(16, '0');
            request.Headers.TryAddWithoutValidation("traceparent", $"00-{traceId}-{spanId}-01");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}