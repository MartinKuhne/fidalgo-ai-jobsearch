using System.Diagnostics;
using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Logging.Configuration;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Tracing;
using Fidalgo.Shared.Tracing.Configuration;
using Fidalgo.Shared.UserProfile;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Fidalgo.Shared.Configuration;

/// <summary>
/// Shared bootstrap helpers for Fidalgo CLI hosts (<c>Fidalgo.Agent</c> and <c>Fidalgo.Ingest</c>),
/// covering help/validation handling, user-profile config precedence, Serilog setup, database path
/// resolution, and root trace/correlation initialization.
/// </summary>
public static class FidalgoHostBootstrap
{
    /// <summary>Returns <c>true</c> if <paramref name="args"/> contains <c>--help</c> or <c>-h</c>.</summary>
    public static bool HasHelpFlag(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return args.Any(a =>
            string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts <see cref="OptionsValidationException"/> instances from an <see cref="AggregateException"/>
    /// when all flattened inner exceptions are options validation failures.
    /// </summary>
    public static bool TryGetValidationExceptions(
        AggregateException aggregateException,
        out List<OptionsValidationException> validationExceptions)
    {
        ArgumentNullException.ThrowIfNull(aggregateException);

        var flattened = aggregateException.Flatten().InnerExceptions;
        validationExceptions = flattened.OfType<OptionsValidationException>().ToList();
        return validationExceptions.Count > 0 && validationExceptions.Count == flattened.Count;
    }

    /// <summary>
    /// Returns <c>true</c> unless user profile configuration loading is explicitly disabled via
    /// <c>UseUserProfileConfig=false</c> or <c>FIDALGO_DISABLE_USER_PROFILE_CONFIG=true</c>.
    /// </summary>
    public static bool ShouldLoadUserProfileConfig(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.Equals(configuration["UseUserProfileConfig"], "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable("FIDALGO_DISABLE_USER_PROFILE_CONFIG"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Adds the user profile configuration JSON source (or <paramref name="userProfileConfigPath"/> when
    /// specified) and inserts it immediately before environment-variable and command-line sources so
    /// environment variables and CLI arguments always take precedence over <c>config.json</c>.
    /// </summary>
    public static void InsertUserProfileConfigBeforeEnvAndCli(
        IConfigurationBuilder configuration,
        string? userProfileConfigPath = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        InsertSourceBeforeEnvAndCli(configuration, builder =>
        {
            if (string.IsNullOrWhiteSpace(userProfileConfigPath))
            {
                builder.AddUserProfileConfig();
            }
            else
            {
                builder.AddJsonFile(userProfileConfigPath, optional: true, reloadOnChange: false);
            }
        });
    }

    /// <summary>
    /// Invokes <paramref name="addSource"/> and moves the newly appended source immediately before the
    /// first <see cref="EnvironmentVariablesConfigurationSource"/> or <see cref="CommandLineConfigurationSource"/>.
    /// </summary>
    public static void InsertSourceBeforeEnvAndCli(
        IConfigurationBuilder configuration,
        Action<IConfigurationBuilder> addSource)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(addSource);

        var countBefore = configuration.Sources.Count;
        addSource(configuration);

        if (configuration.Sources.Count <= countBefore)
        {
            return;
        }

        var addedSource = configuration.Sources[^1];
        var envOrCliIndex = -1;
        for (var i = 0; i < configuration.Sources.Count - 1; i++)
        {
            if (configuration.Sources[i] is EnvironmentVariablesConfigurationSource
                or CommandLineConfigurationSource)
            {
                envOrCliIndex = i;
                break;
            }
        }

        if (envOrCliIndex >= 0)
        {
            configuration.Sources.RemoveAt(configuration.Sources.Count - 1);
            configuration.Sources.Insert(envOrCliIndex, addedSource);
        }
    }

    /// <summary>
    /// Returns the default daily rolling Serilog log file path.
    /// Honors <c>FIDALGO_LOG_DIR</c> when set, falling back to <c>%APPDATA%\fidalgo\logs\log-.json</c>.
    /// </summary>
    public static string GetDefaultLogFilePath()
    {
        var overrideDir = Environment.GetEnvironmentVariable("FIDALGO_LOG_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir))
        {
            return Path.Combine(overrideDir, "log-.json");
        }

        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logDirectory = string.IsNullOrWhiteSpace(appDataRoot)
            ? Path.Combine(AppContext.BaseDirectory, "fidalgo", "logs")
            : Path.Combine(appDataRoot, "fidalgo", "logs");
        return Path.Combine(logDirectory, "log-.json");
    }

    /// <summary>
    /// Configures Serilog with console output and a 7-day rolling compact JSON file sink enriched from
    /// <see cref="LogContext"/> (capturing <c>CorrelationId</c>).
    /// </summary>
    public static void ConfigureFidalgoSerilog(
        IServiceCollection services,
        ILoggingBuilder logging,
        string? logFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logging);

        var effectiveLogPath = string.IsNullOrWhiteSpace(logFilePath)
            ? GetDefaultLogFilePath()
            : logFilePath;
        var logDirectory = Path.GetDirectoryName(effectiveLogPath);
        if (!string.IsNullOrWhiteSpace(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        logging.ClearProviders();
        services.AddSerilog((_, cfg) => cfg
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
    }

    /// <summary>
    /// Resolves <c>DatabasePath</c> from configuration, falling back to <see cref="Constants.GetDefaultDatabasePath"/>
    /// when missing or whitespace.
    /// </summary>
    public static string ResolveDatabasePath(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredDbPath = configuration["DatabasePath"];
        return string.IsNullOrWhiteSpace(configuredDbPath)
            ? Constants.GetDefaultDatabasePath()
            : configuredDbPath;
    }

    /// <summary>
    /// Registers shared Fidalgo logging/tracing infrastructure and ensures the process-wide
    /// <see cref="FidalgoTracingListener"/> is active.
    /// </summary>
    public static IServiceCollection AddFidalgoTelemetry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ILoggingConfiguration, LoggingConfiguration>();
        services.AddSingleton<ITracingConfiguration, TracingConfiguration>();
        services.TryAddSingleton<ITraceContextProvider, TraceContextProvider>();
        services.AddSingleton<ILogEntryWriter, LogEntryWriter>();
        services.AddSingleton<ISpanFactory, SpanFactory>();
        services.AddSingleton<ITraceContextPropagator, TraceContextPropagator>();
        services.AddSingleton<IOtlpExporter, OtlpExporter>();

        FidalgoTracingListener.EnsureRegistered();
        return services;
    }

    /// <summary>
    /// Starts an <see cref="Activity"/> on <paramref name="activitySource"/>, populates
    /// <paramref name="traceContextProvider"/> (if provided), and pushes <c>CorrelationId</c> into
    /// Serilog's <see cref="LogContext"/> for the returned scope lifetime.
    /// </summary>
    public static IDisposable BeginRootTrace(
        ActivitySource activitySource,
        string operationName,
        ITraceContextProvider? traceContextProvider = null)
    {
        ArgumentNullException.ThrowIfNull(activitySource);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        FidalgoTracingListener.EnsureRegistered();

        var activity = activitySource.StartActivity(operationName, ActivityKind.Internal);
        var active = activity ?? Activity.Current;

        var traceId = active is not null && active.TraceId != default
            ? active.TraceId.ToString()
            : Guid.NewGuid().ToString("N");
        var spanId = active is not null && active.SpanId != default
            ? active.SpanId.ToString()
            : Guid.NewGuid().ToString("N")[..16];
        var parentSpanId = active is not null && active.ParentSpanId != default
            ? active.ParentSpanId.ToString()
            : null;

        var previousContext = traceContextProvider?.GetCurrentContext();
        traceContextProvider?.SetCurrentContext(new TraceContext(
            TraceId: traceId,
            SpanId: spanId,
            ParentSpanId: parentSpanId,
            CorrelationId: traceId,
            Timestamp: DateTime.UtcNow,
            IsRoot: parentSpanId is null));

        var logScope = LogContext.PushProperty("CorrelationId", traceId);
        return new TraceScope(activity, logScope, traceContextProvider, previousContext);
    }

    private sealed class TraceScope(
        Activity? activity,
        IDisposable logScope,
        ITraceContextProvider? traceContextProvider,
        TraceContext? previousContext) : IDisposable
    {
        public void Dispose()
        {
            logScope.Dispose();
            activity?.Dispose();
            traceContextProvider?.SetCurrentContext(previousContext);
        }
    }
}
