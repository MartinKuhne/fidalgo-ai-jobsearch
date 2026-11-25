using System.Net;
using System.Text;
using Fidalgo.Shared.Tests.Http;
using Fidalgo.Shared.Tools;
using Microsoft.Extensions.Configuration;
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

    [Fact]
    public async Task SearchAsync_UsesConfiguredSearxngEndpoint_WhenProvided()
    {
        var handler = new HttpMessageHandlerStub();
        handler.QueueJsonResponse("""{"results": []}""");
        var http = new HttpClient(handler);
        var logger = Substitute.For<ILogger<WebSearchTool>>();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalTools:SearxngEndpoint"] = "http://searxng:8080/"
            })
            .Build();

        var tool = new WebSearchTool(http, logger, config);
        await tool.SearchAsync("architect");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("searxng", handler.LastRequest!.RequestUri!.Host);
        Assert.Equal(8080, handler.LastRequest.RequestUri.Port);
    }
}
