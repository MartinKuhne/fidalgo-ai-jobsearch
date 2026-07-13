using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Models;
using NSubstitute;
using Xunit;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Fidalgo.Shared.Tests.Logging;

public class TracingHttpMessageHandlerTests
{
    private readonly ITraceContextProvider _traceContextProvider;

    public TracingHttpMessageHandlerTests()
    {
        _traceContextProvider = Substitute.For<ITraceContextProvider>();
    }

    [Fact]
    public async Task SendAsync_InjectsTraceparentHeader_WhenContextExists()
    {
        var traceId = Guid.NewGuid().ToString("N");
        var spanId = Guid.NewGuid().ToString("N").Substring(0, 16);
        var context = new TraceContext(traceId, spanId, null, "corr1", DateTime.UtcNow, true);
        _traceContextProvider.GetCurrentContext().Returns(context);

        var innerHandler = new TestHandler();
        var handler = new TracingHttpMessageHandler(_traceContextProvider)
        {
            InnerHandler = innerHandler
        };

        var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost");

        await client.SendAsync(request);

        Assert.NotNull(innerHandler.RequestReceived);
        Assert.True(innerHandler.RequestReceived!.Headers.Contains("traceparent"));
        var headerValue = innerHandler.RequestReceived.Headers.GetValues("traceparent").First();
        Assert.StartsWith("00-", headerValue);
        Assert.Contains(traceId, headerValue);
    }

    private class TestHandler : DelegatingHandler
    {
        public HttpRequestMessage? RequestReceived { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestReceived = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}