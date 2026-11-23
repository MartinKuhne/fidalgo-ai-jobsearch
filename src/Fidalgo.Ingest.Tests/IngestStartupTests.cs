using System.Net;
using Fidalgo.Ingest.Configuration;
using Fidalgo.Shared.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fidalgo.Ingest.Tests;

public class IngestStartupTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // Best-effort temp cleanup on Windows.
            }
        }
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fidalgo-ingest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task ExecuteAsync_HelpFlag_PrintsConfigJsonDefaultsAndReturnsZero(string flag)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await Program.ExecuteAsync(
            [flag],
            output: output,
            error: error,
            useUserProfileConfig: false);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains(@"%APPDATA%\.config\Fidalgo\config.json", text);
        Assert.Contains("Usage: Fidalgo.Ingest", text);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroArgs_BindsFromConfigJson_AndRunsApiModeWithoutPrintingHelp()
    {
        var tempDir = CreateTempDir();
        var configPath = Path.Combine(tempDir, "config.json");
        var dbPath = Path.Combine(tempDir, "ingest-test.db");
        var logPath = Path.Combine(tempDir, "logs", "log-.json");

        await File.WriteAllTextAsync(
            configPath,
            $$"""
            {
              "Email": "ingest@example.com",
              "ZipCode": "98101",
              "JobSearchKeywords": ["software architect", "principal engineer"],
              "DatabasePath": "{{dbPath.Replace("\\", "\\\\")}}",
              "Adzuna": {
                "AppId": "test-id",
                "AppKey": "test-key"
              }
            }
            """);

        var capturedUrls = new List<string>();
        var stubHttpClient = new HttpClient(new EmptyAdzunaHandler(capturedUrls));
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await Program.ExecuteAsync(
            [],
            output: output,
            error: error,
            useUserProfileConfig: false,
            userProfileConfigPath: configPath,
            logFilePath: logPath,
            configureServices: services =>
            {
                services.AddSingleton(stubHttpClient);
            });

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("Usage: Fidalgo.Ingest", output.ToString());
        Assert.Single(capturedUrls);
        var decodedUrl = Uri.UnescapeDataString(capturedUrls[0].Replace('+', ' '));
        Assert.Contains("software architect principal engineer", decodedUrl);
        Assert.Contains("where=98101", decodedUrl);

        var logFiles = Directory.GetFiles(Path.Combine(tempDir, "logs"), "log-*.json");
        Assert.NotEmpty(logFiles);
        var logContent = await File.ReadAllTextAsync(logFiles[0]);
        Assert.Contains("Fidalgo.Ingest starting up.", logContent);
        Assert.Contains("Z\"", logContent);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroArgs_WithMissingRequiredConfig_ReturnsExitCode1AndSetupGuidance()
    {
        var tempDir = CreateTempDir();
        var configPath = Path.Combine(tempDir, "empty-config.json");
        await File.WriteAllTextAsync(configPath, "{}");

        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await Program.ExecuteAsync(
            [],
            output: output,
            error: error,
            useUserProfileConfig: false,
            userProfileConfigPath: configPath,
            logFilePath: Path.Combine(tempDir, "logs", "log-.json"),
            configureConfiguration: cfg =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabasePath"] = Path.Combine(tempDir, "ingest-test.db")
                });
            });

        Assert.Equal(1, exitCode);
        var errorText = error.ToString();
        Assert.Contains("config.json", errorText);
        Assert.Contains("Usage: Fidalgo.Ingest", errorText);
    }

    [Fact]
    public void BuildHost_BareBooleanFlags_NormalizeWithoutFormatException()
    {
        var tempDir = CreateTempDir();
        using var host = Program.BuildHost(
            ["--Email", "u@example.com", "--Keywords", "dotnet", "--ZipCode", "98101", "--Scrape", "--QueryJobs"],
            useUserProfileConfig: false,
            logFilePath: Path.Combine(tempDir, "logs", "log-.json"));

        var options = host.Services.GetRequiredService<IOptions<CliOptions>>().Value;

        Assert.Equal("u@example.com", options.Email);
        Assert.Equal("dotnet", options.Keywords);
        Assert.Equal("98101", options.ZipCode);
        Assert.True(options.Scrape);
        Assert.True(options.QueryJobs);
    }

    private sealed class EmptyAdzunaHandler(List<string> capturedUrls) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            capturedUrls.Add(request.RequestUri?.ToString() ?? string.Empty);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"count":0,"results":[]}""")
            });
        }
    }
}
