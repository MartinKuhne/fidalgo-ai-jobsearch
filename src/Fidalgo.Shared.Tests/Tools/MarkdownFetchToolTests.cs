using System.Net;
using Fidalgo.Shared.Tests.Http;
using Fidalgo.Shared.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Fidalgo.Shared.Tests.Tools;

public class MarkdownFetchToolTests
{
    private static (MarkdownFetchTool Tool, HttpMessageHandlerStub Handler) BuildTool()
    {
        var handler = new HttpMessageHandlerStub();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3333/") };
        var logger = Substitute.For<ILogger<MarkdownFetchTool>>();
        return (new MarkdownFetchTool(http, logger), handler);
    }

    [Fact]
    public async Task FetchAsync_ReturnsMarkdownField_WhenResponseHasMarkdownProperty()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse("""{"markdown":"# Hello","url":"https://example.com"}""");

        var result = await tool.FetchAsync("https://example.com");

        Assert.Equal("# Hello", result);
    }

    [Fact]
    public async Task FetchAsync_ReturnsRawJson_WhenResponseMissingMarkdownField()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse("""{"other":"value"}""");

        var result = await tool.FetchAsync("https://example.com");

        Assert.Equal("""{"other":"value"}""", result);
    }

    [Fact]
    public async Task FetchAsync_ReturnsRawJson_WhenResponseIsNotValidJson()
    {
        var (tool, handler) = BuildTool();
        handler.QueueStringResponse("not-json", HttpStatusCode.OK);

        var result = await tool.FetchAsync("https://example.com");

        Assert.Equal("not-json", result);
    }

    [Fact]
    public async Task FetchAsync_ReturnsErrorMessage_WhenHttpCallFails()
    {
        var (tool, handler) = BuildTool();
        handler.QueueException(new HttpRequestException("connection refused"));

        var result = await tool.FetchAsync("https://example.com");

        Assert.StartsWith("Fetch failed:", result);
        Assert.Contains("connection refused", result);
    }

    [Fact]
    public async Task FetchAsync_ReturnsErrorMessage_WhenStatusIsError()
    {
        var (tool, handler) = BuildTool();
        handler.QueueStringResponse("not found", HttpStatusCode.NotFound);

        var result = await tool.FetchAsync("https://example.com");

        Assert.StartsWith("Fetch failed:", result);
    }

    [Fact]
    public async Task FetchAsync_EncodesUrlAndTargetsConfiguredService()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse("""{"markdown":"ok"}""");

        await tool.FetchAsync("https://example.com/path with space");

        Assert.NotNull(handler.LastRequest);
        var uri = handler.LastRequest!.RequestUri!;
        Assert.Equal("/api/fetch", uri.AbsolutePath);
        Assert.Contains("url=https%3A%2F%2Fexample.com%2Fpath%20with%20space", uri.Query);
        Assert.Contains("browser=true", uri.Query);
    }

    [Fact]
    public async Task FetchAsync_HandlesNullMarkdownField()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse("""{"markdown":null}""");

        var result = await tool.FetchAsync("https://example.com");

        Assert.Equal("", result);
    }

    [Fact]
    public async Task FetchAsync_UsesConfiguredMarkdownFetchEndpoint_WhenProvided()
    {
        var handler = new HttpMessageHandlerStub();
        handler.QueueJsonResponse("""{"markdown":"ok"}""");
        var http = new HttpClient(handler);
        var logger = Substitute.For<ILogger<MarkdownFetchTool>>();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalTools:MarkdownFetchEndpoint"] = "http://url-to-markdown:3333/"
            })
            .Build();

        var tool = new MarkdownFetchTool(http, logger, config);
        await tool.FetchAsync("https://example.com");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("url-to-markdown", handler.LastRequest!.RequestUri!.Host);
        Assert.Equal(3333, handler.LastRequest.RequestUri.Port);
    }
}
