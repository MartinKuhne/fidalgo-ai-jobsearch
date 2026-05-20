using Fidalgo.Agent.Tools;
    using Fidalgo.Agent.Models;
    using Microsoft.Extensions.Logging;

namespace Fidalgo.Agent.Tests;

/// <summary>
/// Integration tests for BrowserFetchTool using Playwright with Firefox.
/// Requires Firefox to be installed on the system.
/// </summary>
public class BrowserFetchToolTests
{
    private readonly IBrowserFetchTool _tool;

    public BrowserFetchToolTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<BrowserFetchTool>();
        _tool = new BrowserFetchTool(logger);
    }

    [Fact]
    public async Task FetchAsync_ShouldReturnContent_WhenPageLoadsSuccessfully()
    {
        var request = new FetchRequest(
            Url: "https://example.com",
            TimeoutMilliseconds: 30000);

        var result = await _tool.FetchAsync(request);

        Assert.Null(result.Error);
        Assert.Contains("<html", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.TotalDurationMilliseconds > 0);
    }

    [Fact]
    public async Task FetchAsync_ShouldWaitForSelector_WhenSelectorProvided()
    {
        var request = new FetchRequest(
            Url: "https://example.com",
            WaitForSelector: "h1",
            TimeoutMilliseconds: 30000);

        var result = await _tool.FetchAsync(request);

        Assert.Null(result.Error);
        Assert.True(result.HasWaited);
        Assert.NotNull(result.WaitDurationMilliseconds);
        Assert.True(result.WaitDurationMilliseconds > 0);
    }

    [Fact]
    public async Task FetchAsync_ShouldHandleTimeout_WhenPageTakesTooLong()
    {
        var request = new FetchRequest(
            Url: "https://example.com",
            TimeoutMilliseconds: 1);

        var result = await _tool.FetchAsync(request);

        Assert.NotNull(result.Error);
        Assert.Contains("timed out", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
