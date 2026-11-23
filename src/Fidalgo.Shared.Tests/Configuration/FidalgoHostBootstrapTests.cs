using System.Diagnostics;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Fidalgo.Shared.Tests.Configuration;

public class FidalgoHostBootstrapTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fidalgo-bootstrap-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    [Theory]
    [InlineData(new[] { "--help" }, true)]
    [InlineData(new[] { "-h" }, true)]
    [InlineData(new[] { "--Email", "user@example.com" }, false)]
    public void HasHelpFlag_DetectsHelpSwitches(string[] args, bool expected)
    {
        Assert.Equal(expected, FidalgoHostBootstrap.HasHelpFlag(args));
    }

    [Fact]
    public void TryGetValidationExceptions_ReturnsTrueOnlyWhenAllInnerExceptionsAreValidationFailures()
    {
        var validationOnly = new AggregateException(
            new OptionsValidationException("A", typeof(object), ["bad"]),
            new AggregateException(new OptionsValidationException("B", typeof(object), ["worse"])));

        Assert.True(FidalgoHostBootstrap.TryGetValidationExceptions(validationOnly, out var extracted));
        Assert.Equal(2, extracted.Count);

        var mixed = new AggregateException(
            new OptionsValidationException("A", typeof(object), ["bad"]),
            new InvalidOperationException("other"));

        Assert.False(FidalgoHostBootstrap.TryGetValidationExceptions(mixed, out _));
    }

    [Fact]
    public void InsertUserProfileConfigBeforeEnvAndCli_AllowsEnvVarsAndCliToOverrideConfigJson()
    {
        var tempDir = CreateTempDir();
        var configPath = Path.Combine(tempDir, "config.json");
        File.WriteAllText(configPath, """
        {
          "Email": "from-config@example.com",
          "DatabasePath": "from-config.db"
        }
        """);

        var envKey = $"FIDALGO_BOOTSTRAP_TEST_{Guid.NewGuid():N}";
        var previousEnv = Environment.GetEnvironmentVariable($"{envKey}__DatabasePath");
        Environment.SetEnvironmentVariable($"{envKey}__DatabasePath", "from-env.db");

        try
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email"] = "from-appsettings@example.com"
            });
            builder.AddEnvironmentVariables(prefix: $"{envKey}__");
            builder.AddCommandLine(["--Email", "from-cli@example.com"]);

            FidalgoHostBootstrap.InsertUserProfileConfigBeforeEnvAndCli(builder, configPath);
            var config = builder.Build();

            Assert.Equal("from-cli@example.com", config["Email"]);
            Assert.Equal("from-env.db", config["DatabasePath"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable($"{envKey}__DatabasePath", previousEnv);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveDatabasePath_FallsBackToDefault_WhenMissingOrWhitespace(string? configuredPath)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabasePath"] = configuredPath
            })
            .Build();

        Assert.Equal(Constants.GetDefaultDatabasePath(), FidalgoHostBootstrap.ResolveDatabasePath(config));
    }

    [Fact]
    public void ResolveDatabasePath_ReturnsConfiguredPath_WhenNonEmpty()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabasePath"] = @"C:\custom\jobs.db"
            })
            .Build();

        Assert.Equal(@"C:\custom\jobs.db", FidalgoHostBootstrap.ResolveDatabasePath(config));
    }

    [Fact]
    public void BeginRootTrace_StartsActivityAndPopulatesAndRestoresTraceContext()
    {
        using var source = new ActivitySource("Fidalgo.BootstrapTest");
        var provider = new TraceContextProvider();
        var original = new TraceContext("origtrace", "origspan", null, "origcorr", DateTime.UtcNow, true);
        provider.SetCurrentContext(original);

        using (FidalgoHostBootstrap.BeginRootTrace(source, "bootstrap.test", provider))
        {
            Assert.NotNull(Activity.Current);
            var current = provider.GetCurrentContext();
            Assert.NotNull(current);
            Assert.Equal(Activity.Current!.TraceId.ToString(), current!.TraceId);
            Assert.Equal(current.TraceId, current.CorrelationId);
        }

        Assert.Same(original, provider.GetCurrentContext());
    }
}
