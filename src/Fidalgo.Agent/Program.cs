using Fidalgo.Agent;
using Fidalgo.Agent.Configuration;
using Fidalgo.Agent.DependencyInjection;
using Fidalgo.Shared.ApplicationPackets;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.Llm;
using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Logging.Configuration;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.Tracing;
using Fidalgo.Shared.Tracing.Configuration;
using Fidalgo.Shared.UserProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

return await Program.ExecuteAsync(args);

/// <summary>
/// Entry point and host builder for <c>Fidalgo.Agent</c>.
/// </summary>
public partial class Program
{
    internal static readonly HashSet<string> BooleanFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "--QueryJobs",
        "--ListDiscarded",
        "--GeneratePackets",
        "--ForceRegenerate",
        "--SkipExisting"
    };

    /// <summary>
    /// Executes the <c>Fidalgo.Agent</c> CLI workflow.
    /// </summary>
    public static async Task<int> ExecuteAsync(
        string[] args,
        TextWriter? output = null,
        TextWriter? error = null,
        bool? useUserProfileConfig = null,
        string? userProfileConfigPath = null,
        string? logFilePath = null,
        Action<IConfigurationBuilder>? configureConfiguration = null,
        Action<IServiceCollection>? configureServices = null,
        CancellationToken cancellationToken = default)
    {
        var outWriter = output ?? Console.Out;
        var errWriter = error ?? Console.Error;

        if (HasHelpFlag(args))
        {
            PrintUsage(outWriter);
            return 0;
        }

        using var host = BuildHost(
            args,
            output: outWriter,
            useUserProfileConfig: useUserProfileConfig,
            userProfileConfigPath: userProfileConfigPath,
            logFilePath: logFilePath,
            configureConfiguration: configureConfiguration,
            configureServices: configureServices);

        try
        {
            await host.StartAsync(cancellationToken);

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Fidalgo.Agent starting up.");

            await using (var initScope = host.Services.CreateAsyncScope())
            {
                var db = initScope.ServiceProvider.GetRequiredService<JobDbContext>();
                await db.Database.EnsureCreatedAsync(cancellationToken);
            }

            var cliOptions = host.Services.GetRequiredService<IOptions<CliOptions>>().Value;

            int exitCode;
            await using (var runScope = host.Services.CreateAsyncScope())
            {
                var runner = runScope.ServiceProvider.GetRequiredService<AgentRunner>();
                exitCode = await runner.RunAsync(cliOptions, cancellationToken);
            }

            logger.LogInformation("Fidalgo.Agent shutting down with exit code {ExitCode}.", exitCode);
            await host.StopAsync(cancellationToken);
            return exitCode;
        }
        catch (OptionsValidationException ex)
        {
            await WriteValidationErrorsAsync([ex], errWriter);
            return 1;
        }
        catch (AggregateException ex) when (TryGetValidationExceptions(ex, out var validationExceptions))
        {
            await WriteValidationErrorsAsync(validationExceptions, errWriter);
            return 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 1;
        }
        catch (Exception ex)
        {
            var logger = host.Services.GetService<ILogger<Program>>();
            logger?.LogError(ex, "Unexpected exception terminated Fidalgo.Agent.");
            await errWriter.WriteLineAsync($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Builds a configured <see cref="IHost"/> for <c>Fidalgo.Agent</c> with normalized boolean flags,
    /// shared LLM/packet DI registrations, and per-host Serilog logging.
    /// </summary>
    public static IHost BuildHost(
        string[] args,
        TextWriter? output = null,
        bool? useUserProfileConfig = null,
        string? userProfileConfigPath = null,
        string? logFilePath = null,
        Action<IConfigurationBuilder>? configureConfiguration = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var normalizedArgs = CliArgumentNormalizer.NormalizeBooleanFlags(args, BooleanFlags);
        var builder = Host.CreateApplicationBuilder(normalizedArgs);

        var baseDir = AppContext.BaseDirectory;
        builder.Configuration.AddJsonFile(Path.Combine(baseDir, "appsettings.json"), optional: false, reloadOnChange: true);
        builder.Configuration.AddJsonFile(Path.Combine(baseDir, "appsettings.Development.json"), optional: true, reloadOnChange: true);

        var loadUserProfile = useUserProfileConfig ?? ShouldLoadUserProfileConfig(builder.Configuration);
        if (loadUserProfile)
        {
            builder.Configuration.AddUserProfileConfig();
        }
        else if (!string.IsNullOrWhiteSpace(userProfileConfigPath))
        {
            builder.Configuration.AddJsonFile(userProfileConfigPath, optional: true, reloadOnChange: false);
        }

        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets<Program>(optional: true);
        }

        configureConfiguration?.Invoke(builder.Configuration);
        builder.Configuration.AddCommandLine(normalizedArgs);

        builder.Services.AddCliOptions(builder.Configuration);
        builder.Services.AddAgentServices(builder.Configuration.GetSection("DatabasePath")?.Get<string>());
        builder.Services.AddFidalgoLlmClient(builder.Configuration);
        builder.Services.AddApplicationPacketServices(builder.Configuration);

        if (output is not null)
        {
            builder.Services.AddSingleton(output);
        }

        builder.Services.AddSingleton<ILoggingConfiguration, LoggingConfiguration>();
        builder.Services.AddSingleton<ITracingConfiguration, TracingConfiguration>();
        builder.Services.TryAddSingleton<ITraceContextProvider, TraceContextProvider>();
        builder.Services.AddSingleton<ILogEntryWriter, LogEntryWriter>();
        builder.Services.AddSingleton<ISpanFactory, SpanFactory>();
        builder.Services.AddSingleton<ITraceContextPropagator, TraceContextPropagator>();
        builder.Services.AddSingleton<IOtlpExporter, OtlpExporter>();

        var effectiveLogPath = logFilePath ?? GetDefaultLogFilePath();
        var logDirectory = Path.GetDirectoryName(effectiveLogPath);
        if (!string.IsNullOrWhiteSpace(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((_, cfg) => cfg
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: effectiveLogPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true));

        configureServices?.Invoke(builder.Services);

        return builder.Build();
    }

    internal static bool HasHelpFlag(string[] args) =>
        args.Any(a =>
            string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase));

    internal static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Fidalgo JobSearch AI Agent");
        writer.WriteLine("Usage: Fidalgo.Agent [options]");
        writer.WriteLine();
        writer.WriteLine("Configuration & Profile Defaults:");
        writer.WriteLine("  Config file:      %APPDATA%\\.config\\Fidalgo\\config.json (Email, ApplicationPacket, LLM)");
        writer.WriteLine("  Profile files:    Documents\\Fidalgo\\resume.md, career-story.md, career-goals.md");
        writer.WriteLine("                    (falls back to %APPDATA%\\.config\\Fidalgo\\ if not yet migrated)");
        writer.WriteLine("  Packet output:    Documents\\Fidalgo\\applications\\<employer>_<title>_<short-id>\\");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --Email <email>                 [Required if not in config.json] Tenant email address");
        writer.WriteLine("  --Resume <path>                 Optional path to base resume (default: Documents\\Fidalgo\\resume.md)");
        writer.WriteLine("  --CareerStory <path>            Optional path to career story (default: Documents\\Fidalgo\\career-story.md)");
        writer.WriteLine("  --CareerGoals <path>            Optional path to career goals (default: Documents\\Fidalgo\\career-goals.md)");
        writer.WriteLine("  --GeneratePackets               Generate application packets for matching evaluated jobs");
        writer.WriteLine("  --TargetJobId <guid>            Generate application packet for a single job ID");
        writer.WriteLine("  --ForceRegenerate               Overwrite existing complete packets when generating");
        writer.WriteLine("  --SkipExisting <bool>           Skip complete existing packets (default: true)");
        writer.WriteLine("  --OutputDir <path>              Override output base directory for generated packets");
        writer.WriteLine("  --RecommendationFilter <tiers>  Comma-separated recommendation tiers (default: Apply)");
        writer.WriteLine("  --QueryJobs                     Query mode: List saved jobs");
        writer.WriteLine("  --EmployerFilter <name>         Query mode: Filter jobs by employer");
        writer.WriteLine("  --DateFrom <date>               Query mode: Filter jobs from this date");
        writer.WriteLine("  --DateTo <date>                 Query mode: Filter jobs to this date");
        writer.WriteLine("  --SourceWebsiteFilter <site>    Query mode: Filter by job board source");
        writer.WriteLine("  --DiscardJobId <guid>           Discard mode: Mark a job as discarded");
        writer.WriteLine("  --ListDiscarded                 List mode: Show all discarded jobs");
        writer.WriteLine("  --help, -h                      Show this help message");
    }

    private static bool TryGetValidationExceptions(
        AggregateException aggregateException,
        out List<OptionsValidationException> validationExceptions)
    {
        var flattened = aggregateException.Flatten().InnerExceptions;
        validationExceptions = flattened.OfType<OptionsValidationException>().ToList();
        return validationExceptions.Count > 0 && validationExceptions.Count == flattened.Count;
    }

    private static async Task WriteValidationErrorsAsync(
        IEnumerable<OptionsValidationException> exceptions,
        TextWriter errWriter)
    {
        await errWriter.WriteLineAsync("Configuration error: required settings are missing or invalid.");
        foreach (var failure in exceptions.SelectMany(e => e.Failures))
        {
            await errWriter.WriteLineAsync($"  - {failure}");
        }

        await errWriter.WriteLineAsync();
        await errWriter.WriteLineAsync(
            $"Configure defaults in {UserProfileConfig.GetDefaultConfigPath()} (e.g. \"Email\") or pass --Email <email>.");
        await errWriter.WriteLineAsync();
        PrintUsage(errWriter);
    }

    private static bool ShouldLoadUserProfileConfig(IConfiguration configuration)
    {
        if (string.Equals(configuration["UseUserProfileConfig"], "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("FIDALGO_DISABLE_USER_PROFILE_CONFIG"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string GetDefaultLogFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "fidalgo", "logs", "log-.json");
    }
}
