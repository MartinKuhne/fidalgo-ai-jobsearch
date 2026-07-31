using System.Net;
using System.Text;
using System.Text.Json;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.Tests.Http;
using Fidalgo.Shared.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Fidalgo.Shared.Tests.Tools;

public class JobSearchToolTests
{
    private static (JobSearchTool Tool, HttpMessageHandlerStub Handler) BuildTool(IOptions<AdzunaConfiguration>? config = null)
    {
        var handler = new HttpMessageHandlerStub();
        var http = new HttpClient(handler);
        var logger = Substitute.For<ILogger<JobSearchTool>>();
        var options = config ?? Options.Create(new AdzunaConfiguration
        {
            AppId = "test-app-id",
            AppKey = "test-app-key",
        });
        return (new JobSearchTool(http, options, logger), handler);
    }

    private static string SampleResults(int count = 1)
    {
        var jobs = Enumerable.Range(0, count).Select(i => new
        {
            id = $"job-{i}",
            title = $"Software Engineer {i}",
            company = new { display_name = $"Acme {i}" },
            location = new { display_name = "Seattle, WA" },
            salary_min = 100000m + i,
            salary_max = 150000m + i,
            contract_type = "permanent",
            contract_time = "full_time",
            category = new { label = "IT Jobs" },
            created = "2026-05-01T00:00:00Z",
            redirect_url = $"https://example.com/jobs/{i}",
            description = "Great job",
        });
        var response = new
        {
            results = jobs,
            count,
            total_results = count,
            results_per_page = count,
            from = 0,
            to = count,
        };
        return JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
    }

    [Fact]
    public async Task SearchAsync_ReturnsFormattedJobs_WhenApiReturnsResults()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse(SampleResults(2));

        var result = await tool.SearchAsync("software engineer", "Seattle");

        Assert.Contains("Found 2 jobs for 'software engineer' in Seattle", result);
        Assert.Contains("## Software Engineer 0", result);
        Assert.Contains("## Software Engineer 1", result);
        Assert.Contains("Acme 0", result);
        Assert.Contains("URL: https://example.com/jobs/0", result);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNoJobsMessage_WhenResultsEmpty()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse("""{"results":[],"count":0}""");

        var result = await tool.SearchAsync("niche", "Nowhere");

        Assert.Equal("No jobs found for 'niche' in Nowhere.", result);
    }

    [Fact]
    public async Task SearchAsync_BuildsExpectedUrl()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse(SampleResults());

        await tool.SearchAsync("software engineer", "Seattle");

        Assert.NotNull(handler.LastRequest);
        var uri = handler.LastRequest!.RequestUri!;
        Assert.Equal("/v1/api/jobs/us/search/1", uri.AbsolutePath);
        Assert.Contains("app_id=test-app-id", uri.Query);
        Assert.Contains("app_key=test-app-key", uri.Query);
        Assert.Contains("what=software%20engineer", uri.Query);
        Assert.Contains("where=Seattle", uri.Query);
        Assert.Contains("results_per_page=20", uri.Query);
    }

    [Fact]
    public async Task SearchAsync_ClampsResultsPerPageToValidRange()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse(SampleResults());

        await tool.SearchAsync("k", "l", resultsPerPage: 0);
        Assert.Contains("results_per_page=1", handler.LastRequest!.RequestUri!.Query);

        await tool.SearchAsync("k", "l", resultsPerPage: 9999);
        Assert.Contains("results_per_page=100", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task SearchAsync_IncludesSalaryMin_WhenPositive()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse(SampleResults());

        await tool.SearchAsync("k", "l", salaryMin: 80000m);

        Assert.Contains("salary_min=80000", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task SearchAsync_OmitsSalaryMin_WhenZeroOrNull()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse(SampleResults());
        handler.QueueJsonResponse(SampleResults());

        await tool.SearchAsync("k", "l", salaryMin: 0);
        Assert.DoesNotContain("salary_min", handler.LastRequest!.RequestUri!.Query);

        await tool.SearchAsync("k", "l", salaryMin: null);
        Assert.DoesNotContain("salary_min", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task SearchAsync_AddsContractAndPermanentFlags()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse(SampleResults());
        handler.QueueJsonResponse(SampleResults());

        await tool.SearchAsync("k", "l", contractType: "contract");
        Assert.Contains("contract=1", handler.LastRequest!.RequestUri!.Query);

        await tool.SearchAsync("k", "l", contractType: "permanent");
        Assert.Contains("permanent=1", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task SearchAsync_AddsFullTimeAndPartTimeFlags()
    {
        var (tool, handler) = BuildTool();
        handler.QueueJsonResponse(SampleResults());
        handler.QueueJsonResponse(SampleResults());

        await tool.SearchAsync("k", "l", contractTime: "full-time");
        Assert.Contains("full_time=1", handler.LastRequest!.RequestUri!.Query);

        await tool.SearchAsync("k", "l", contractTime: "part time");
        Assert.Contains("part_time=1", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyMessage_WhenApiReturnsError()
    {
        var (tool, handler) = BuildTool();
        handler.QueueStringResponse("upstream broken", HttpStatusCode.InternalServerError);

        var result = await tool.SearchAsync("k", "l");

        Assert.Equal("No jobs found for 'k' in l.", result);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyMessage_WhenNetworkFails()
    {
        var (tool, handler) = BuildTool();
        handler.QueueException(new HttpRequestException("connection refused"));

        var result = await tool.SearchAsync("k", "l");

        Assert.Equal("No jobs found for 'k' in l.", result);
    }

    [Fact]
    public async Task SearchAsync_HandlesSalaryMinOnly()
    {
        var (tool, handler) = BuildTool();
        var json = """{"results":[{"id":"j1","title":"X","description":"d","salary_min":50000}],"count":1}""";
        handler.QueueJsonResponse(json);

        var result = await tool.SearchAsync("k", "l");

        Assert.Contains("Salary:", result);
        Assert.DoesNotContain(" - ", result);
    }

    [Fact]
    public async Task SearchAsync_HandlesMissingSalary()
    {
        var (tool, handler) = BuildTool();
        var json = """{"results":[{"id":"j1","title":"X","description":"d"}],"count":1}""";
        handler.QueueJsonResponse(json);

        var result = await tool.SearchAsync("k", "l");

        Assert.Contains("Salary: Not specified", result);
    }

    [Fact]
    public async Task SearchAsync_HandlesNullDescription()
    {
        var (tool, handler) = BuildTool();
        var json = """{"results":[{"id":"j1","title":"X","description":null}],"count":1}""";
        handler.QueueJsonResponse(json);

        var result = await tool.SearchAsync("k", "l");

        Assert.Contains("## X", result);
    }
}
