using System.Net;
using System.Text;
using Fidalgo.Shared.Tests.Http;
using Fidalgo.Shared.Tools;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Fidalgo.Shared.Tests.Tools;

public class WebSearchToolTests
{
    private static (WebSearchTool Tool, HttpMessageHandlerStub Handler) BuildTool()
    {
        var handler = new HttpMessageHandlerStub();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8090/") };
        var logger = Substitute.For<ILogger<WebSearchTool>>();
        return (new WebSearchTool(http, logger), handler);
    }

    [Fact]
    public async Task SearchAsync_FormatsResults_WhenSearchSucceeds()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse("""
            {
              "results": [
                { "title": "First", "url": "https://a.example", "content": "Snippet one" },
                { "title": "Second", "url": "https://b.example", "content": "Snippet two" }
              ]
            }
            """);

        var result = await tool.SearchAsync("dotnet testing");

        Assert.Contains("## First", result);
        Assert.Contains("URL: https://a.example", result);
        Assert.Contains("Snippet one", result);
        Assert.Contains("## Second", result);
        Assert.Contains("Snippet two", result);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNoResultsMessage_WhenResultsKeyMissing()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse("""{"hits": []}""");

        var result = await tool.SearchAsync("dotnet");

        Assert.Equal("No results found.", result);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNoResultsMessage_WhenResultsArrayEmpty()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse("""{"results": []}""");

        var result = await tool.SearchAsync("dotnet");

        Assert.Equal("No results found.", result);
    }

    [Fact]
    public async Task SearchAsync_HandlesMissingOptionalFields()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse("""{"results": [{}]}""");

        var result = await tool.SearchAsync("dotnet");

        Assert.Contains("## ", result);
        Assert.Contains("URL: ", result);
    }

    [Fact]
    public async Task SearchAsync_ReturnsErrorMessage_WhenHttpFails()
    {
        var (tool, handler) = BuildTool();
        handler.QueueException(new HttpRequestException("connection refused"));

        var result = await tool.SearchAsync("dotnet");

        Assert.StartsWith("Search failed:", result);
        Assert.Contains("connection refused", result);
    }

    [Fact]
    public async Task SearchAsync_ReturnsErrorMessage_WhenStatusIsError()
    {
        var (tool, handler) = BuildTool();
        handler.QueueStringResponse("upstream broke", HttpStatusCode.BadGateway);

        var result = await tool.SearchAsync("dotnet");

        Assert.StartsWith("Search failed:", result);
    }

    [Fact]
    public async Task SearchAsync_EncodesQuery()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse("""{"results": []}""");

        await tool.SearchAsync("hello world & friends");

        Assert.NotNull(handler.LastRequest);
        Assert.Contains("q=hello%20world%20%26%20friends", handler.LastRequest!.RequestUri!.Query);
        Assert.Contains("format=json", handler.LastRequest.RequestUri.Query);
    }
}
