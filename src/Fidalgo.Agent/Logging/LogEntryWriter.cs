using Fidalgo.Agent.Models;
using Microsoft.Extensions.Logging;

namespace Fidalgo.Agent.Logging;

public class LogEntryWriter : ILogEntryWriter
{
    private readonly ILogger<LogEntryWriter> _logger;
    private readonly ITraceContextProvider _traceContextProvider;

    public LogEntryWriter(ILogger<LogEntryWriter> logger, ITraceContextProvider traceContextProvider)
    {
        _logger = logger;
        _traceContextProvider = traceContextProvider;
    }

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

    public async Task WriteAsync(
        Fidalgo.Agent.Models.LogLevel level,
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
            properties?.ToDictionary(p => p.Key, p => p.Value));

        await WriteAsync(logEntry, cancellationToken);
    }

    public async Task WriteErrorAsync(
        Exception exception,
        string message,
        string traceId,
        string spanId,
        string correlationId,
        IDictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default)
    {
        var exceptionDetails = new ExceptionDetails(
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            exception.StackTrace ?? string.Empty,
            exception.InnerException != null ? new ExceptionDetails(
                exception.InnerException.GetType().FullName ?? exception.InnerException.GetType().Name,
                exception.InnerException.Message,
                exception.InnerException.StackTrace ?? string.Empty,
                exception.InnerException.InnerException != null ? new ExceptionDetails(
                    exception.InnerException.InnerException.GetType().FullName ?? exception.InnerException.InnerException.GetType().Name,
                    exception.InnerException.InnerException.Message,
                    exception.InnerException.InnerException.StackTrace ?? string.Empty,
                    null,
                    null) : null,
                exception.InnerException.Source) : null,
            exception.Source);

        var logEntry = new LogEntry(
            DateTime.UtcNow,
            Fidalgo.Agent.Models.LogLevel.Error,
            message,
            traceId,
            spanId,
            correlationId,
            exceptionDetails,
            properties?.ToDictionary(p => p.Key, p => p.Value));

        await WriteAsync(logEntry, cancellationToken);
    }

    private static Microsoft.Extensions.Logging.LogLevel ToMicrosoftLogLevel(Fidalgo.Agent.Models.LogLevel level)
    {
        return level switch
        {
            Fidalgo.Agent.Models.LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
            Fidalgo.Agent.Models.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            Fidalgo.Agent.Models.LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
            Fidalgo.Agent.Models.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            Fidalgo.Agent.Models.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            Fidalgo.Agent.Models.LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };
    }
}
