using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Fidalgo.Ingest.Configuration;
using Fidalgo.Ingest.DependencyInjection;
using Fidalgo.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Fidalgo.Ingest.Tests.Integration;

public class ApiModeRunnerTests
{
    [Fact]
    public async Task RunAsync_ApiMode_FetchesAndSavesJobs()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Use in-memory SQLite for testing
        // For true in-memory SQLite that persists across context creations, use a shared connection
        var dbName = $"{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        services.AddAgentServices(dbName);

        // Add Configuration
        var inMemorySettings = new Dictionary<string, string>
        {
            {"Adzuna:AppId", "test_id"},
            {"Adzuna:AppKey", "test_key"}
        };
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);
        
        // Add Logging
        services.AddLogging();
        
        // Mock HttpMessageHandler
        var handlerMock = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handlerMock);
        
        // Replace HttpClient with our mocked one
        services.AddSingleton(httpClient);

        // Build Service Provider
        var serviceProvider = services.BuildServiceProvider();

        // Initialize Database and KEEP SCOPE OPEN so SQLite in-memory shared database isn't destroyed
        using var dbScope = serviceProvider.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<JobDbContext>();
        db.Database.OpenConnection();
        await db.Database.EnsureCreatedAsync();

        var options = new CliOptions
        {
            Email = "test@example.com",
            Keywords = "test engineer",
            ZipCode = "10001",
            Scrape = false
        };

        var runner = serviceProvider.GetRequiredService<ApiModeRunner>();

        // Act
        await runner.RunAsync(options);

        // Assert
        var jobs = await db.Jobs.ToListAsync();
        
        Assert.Equal(2, jobs.Count);
        
        Assert.Contains(jobs, j => j.Title == "Senior Test Engineer" && j.Employer == "TestCorp");
        Assert.Contains(jobs, j => j.Title == "QA Automation" && j.Employer == "QualityInc");
        
        // Should be saved as untriaged (score = 0, recommendation = Maybe)
        Assert.All(jobs, j => Assert.Equal(0, j.Score));
        Assert.All(jobs, j => Assert.Equal("Maybe", j.Recommendation));
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? string.Empty;
        
        if (url.Contains("adzuna.com"))
        {
            var response = new
            {
                count = 2,
                results = new[]
                {
                    new 
                    {
                        id = "1001",
                        title = "Senior Test Engineer",
                        company = new { display_name = "TestCorp" },
                        redirect_url = "https://adzuna.com/job/1001",
                        created = "2026-07-11T12:00:00Z"
                    },
                    new 
                    {
                        id = "1002",
                        title = "QA Automation",
                        company = new { display_name = "QualityInc" },
                        redirect_url = "https://adzuna.com/job/1002",
                        created = "2026-07-11T13:00:00Z"
                    }
                }
            };
            
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response))
            });
        }
        
        if (url.Contains("localhost:3333"))
        {
            // Markdown fetch mock
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Mocked markdown content")
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
