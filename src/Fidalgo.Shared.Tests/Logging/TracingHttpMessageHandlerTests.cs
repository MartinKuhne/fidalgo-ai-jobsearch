using System.Diagnostics;
using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Tracing;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendAsync_FallsBackToActivityCurrent_WhenTraceContextIsMissingOrEmpty(bool useEmptyTraceIdContext)
    {
        _traceContextProvider.GetCurrentContext().Returns(
            useEmptyTraceIdContext
                ? new TraceContext(string.Empty, string.Empty, null, "corr", DateTime.UtcNow, true)
                : null);

        var previousActivity = Activity.Current;
        Activity.Current = null;
        try
        {
            using var activity = new Activity("test.outgoing")
                .SetIdFormat(ActivityIdFormat.W3C);
            activity.TraceStateString = "fidalgo=corr-123";
            activity.Start();

            var innerHandler = new TestHandler();
            using var handler = new TracingHttpMessageHandler(_traceContextProvider)
            {
                InnerHandler = innerHandler
            };
            using var client = new HttpClient(handler);

            await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost"));

            Assert.NotNull(innerHandler.RequestReceived);
            var traceparent = innerHandler.RequestReceived!.Headers.GetValues("traceparent").Single();
            Assert.Equal($"00-{activity.TraceId}-{activity.SpanId}-01", traceparent);
            var tracestate = innerHandler.RequestReceived.Headers.GetValues("tracestate").Single();
            Assert.Equal("fidalgo=corr-123", tracestate);
        }
        finally
        {
            Activity.Current = previousActivity;
        }
    }

    [Fact]
    public async Task SendAsync_OmitsTraceHeaders_WhenBothContextAndActivityAreNull()
    {
        _traceContextProvider.GetCurrentContext().Returns((TraceContext?)null);
        var previousActivity = Activity.Current;
        Activity.Current = null;
        try
        {
            var innerHandler = new TestHandler();
            using var handler = new TracingHttpMessageHandler(_traceContextProvider)
            {
                InnerHandler = innerHandler
            };
            using var client = new HttpClient(handler);

            await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost"));

            Assert.NotNull(innerHandler.RequestReceived);
            Assert.False(innerHandler.RequestReceived!.Headers.Contains("traceparent"));
            Assert.False(innerHandler.RequestReceived.Headers.Contains("tracestate"));
        }
        finally
        {
            Activity.Current = previousActivity;
        }
    }

    [Fact]
    public void FidalgoTracingListener_EnsureRegistered_EnablesActivitiesForFidalgoSources()
    {
        FidalgoTracingListener.EnsureRegistered();
        FidalgoTracingListener.EnsureRegistered();

        using var source = new ActivitySource("Fidalgo.ApplicationPacket");
        using var activity = source.StartActivity("test.span", ActivityKind.Internal);

        Assert.NotNull(activity);
        Assert.NotEqual(default, activity!.TraceId);
        Assert.NotEqual(default, activity.SpanId);
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