using Fidalgo.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Shared.Logging;

/// <summary>
/// Bridges internal LogEntry model to Microsoft.Extensions.Logging infrastructure.
/// Maps custom log levels, extracts exception details hierarchically, and handles trace context.
/// </summary>
public class LogEntryWriter : ILogEntryWriter
{
    private readonly ILogger<LogEntryWriter> _logger;
    private readonly ITraceContextProvider _traceContextProvider;

    /// <summary>Initializes a new instance of the LogEntryWriter.</summary>
    /// <param name="logger">Microsoft.Extensions.Logging logger instance.</param>
    /// <param name="traceContextProvider">Provider for trace context.</param>
    public LogEntryWriter(ILogger<LogEntryWriter> logger, ITraceContextProvider traceContextProvider)
    {
        _logger = logger;
        _traceContextProvider = traceContextProvider;
    }

    /// <summary>Writes a structured log entry to the logging infrastructure.</summary>
    public async Task WriteAsync(LogEntry logEntry, CancellationToken cancellationToken = default)
    {
        var context = _traceContextProvider.GetCurrentContext();
        if (context == null)
        {
            context = new TraceContext(
                Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N")[..16],
                null,
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                true);
        }

        var logLevel = ToMicrosoftLogLevel(logEntry.Level);
        _logger.Log(logLevel, logEntry.Message);

        await Task.CompletedTask;
    }

    /// <summary>Writes a log entry with individual parameters.</summary>
    public async Task WriteAsync(
        Fidalgo.Shared.Models.LogLevel level,
        string message,
        string traceId,
        string spanId,
        string correlationId,
        ExceptionDetails? exception = null,
        IDictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default)
    {
        var logEntry = new LogEntry(
            DateTime.UtcNow,
            level,
            message,
            traceId,
            spanId,
            correlationId,
            exception,
            properties?.ToDictionary(p => p.Key, p => p.Value).AsReadOnly());

        await WriteAsync(logEntry, cancellationToken);
    }

    /// <summary>Writes an error log entry with exception details.</summary>
    public async Task WriteErrorAsync(
        Exception exception,
        string message,
        string traceId,
        string spanId,
        string correlationId,
        IDictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default)
    {
        var exceptionDetails = MapException(exception);

        var logEntry = new LogEntry(
            DateTime.UtcNow,
            Fidalgo.Shared.Models.LogLevel.Error,
            message,
            traceId,
            spanId,
            correlationId,
            exceptionDetails,
            properties?.ToDictionary(p => p.Key, p => p.Value).AsReadOnly());

        await WriteAsync(logEntry, cancellationToken);
    }

    /// <summary>Maps a custom log level to the corresponding Microsoft.Extensions.Logging level.</summary>
    private static Microsoft.Extensions.Logging.LogLevel ToMicrosoftLogLevel(Fidalgo.Shared.Models.LogLevel level)
    {
        return level switch
        {
            Fidalgo.Shared.Models.LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
            Fidalgo.Shared.Models.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            Fidalgo.Shared.Models.LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
            Fidalgo.Shared.Models.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            Fidalgo.Shared.Models.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            Fidalgo.Shared.Models.LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };
    }

    /// <summary>Pure recursive function to map exceptions to details.</summary>
    private static ExceptionDetails? MapException(Exception? exception, int maxDepth = 10, int currentDepth = 0)
    {
        if (exception == null || currentDepth >= maxDepth)
        {
            return null;
        }

        return new ExceptionDetails(
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            exception.StackTrace ?? string.Empty,
            MapException(exception.InnerException, maxDepth, currentDepth + 1),
            exception.Source);
    }
}