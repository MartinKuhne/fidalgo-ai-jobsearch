using System.Net;

namespace Fidalgo.Shared.Tests.Http;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/> that returns a queued
/// response (or throws a queued exception) for each request. Used to exercise
/// HTTP-based tools without touching the network.
/// </summary>
public class HttpMessageHandlerStub : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    /// <summary>Number of requests the handler has processed.</summary>
    public int CallCount { get; private set; }

    /// <summary>The most recent request the handler observed.</summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>Queues a JSON response body and a 200 OK status for the next request.</summary>
    public HttpMessageHandlerStub QueueJsonResponse(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        });
        return this;
    }

    /// <summary>Queues a non-JSON response (e.g. plain text) with the given status code.</summary>
    public HttpMessageHandlerStub QueueStringResponse(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "text/plain"),
        });
        return this;
    }

    /// <summary>Queues an exception to be thrown from SendAsync for the next request.</summary>
    public HttpMessageHandlerStub QueueException(Exception ex)
    {
        _responses.Enqueue(_ => throw ex);
        return this;
    }

    /// <summary>Queues a response generated lazily from the outgoing request.</summary>
    public HttpMessageHandlerStub QueueResponse(Func<HttpRequestMessage, HttpResponseMessage> factory)
    {
        _responses.Enqueue(req => factory(req));
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No queued response. Use QueueJsonResponse/QueueException before invoking the tool.");
        }
        var result = _responses.Dequeue()(request);
        return Task.FromResult(result);
    }
}
