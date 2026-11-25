using Fidalgo.Shared.Logging;

namespace Fidalgo.Shared.Pdf;

/// <summary>
/// Owns the singleton <see cref="HttpClient"/> and handler pipeline used by <see cref="MdToPdfRenderer"/>
/// so the DI container cleanly disposes them on shutdown.
/// </summary>
internal sealed class OwnedMdToPdfHttpClient : IDisposable
{
    public OwnedMdToPdfHttpClient(ITraceContextProvider traceContextProvider)
    {
        ArgumentNullException.ThrowIfNull(traceContextProvider);

        var handler = new TracingHttpMessageHandler(traceContextProvider)
        {
            InnerHandler = new SocketsHttpHandler()
        };

        Client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public HttpClient Client { get; }

    public void Dispose()
    {
        Client.Dispose();
    }
}
