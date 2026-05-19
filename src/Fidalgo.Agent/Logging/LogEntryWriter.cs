using Fidalgo.Agent.Models;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Fidalgo.Agent.Logging;

public class LogEntryWriter : ILogEntryWriter
{
    private readonly ILogger _logger;
    private readonly ITraceContextProvider _traceContextProvider;

    public LogEntryWriter(ILogger logger, ITraceContextProvider traceContextProvider)
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

        var properties = new Dictionary<string, object?>
        {
            ["TraceId"] = context.TraceId,
            ["SpanId"] = context.SpanId,
            ["CorrelationId"] = context.CorrelationId,
            ["Timestamp"] = logEntry.Timestamp,
            ["Level"] = logEntry.Level.ToString()
        };

        if (logEntry.Properties != null)
        {
            foreach (var prop in logEntry.Properties)
            {
                properties[prop.Key] = prop.Value;
            }
        }

        if (logEntry.Exception != null)
        {
            properties["Exception"] = SerializeExceptionDetails(logEntry.Exception);
        }

        var logEventLevel = ToSerilogLevel(logEntry.Level);
        _logger.Write(logEventLevel, logEntry.Message, properties.Values.ToArray());
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

    private static LogEventLevel ToSerilogLevel(Fidalgo.Agent.Models.LogLevel level)
    {
        return level switch
        {
            Fidalgo.Agent.Models.LogLevel.Trace => LogEventLevel.Verbose,
            Fidalgo.Agent.Models.LogLevel.Debug => LogEventLevel.Debug,
            Fidalgo.Agent.Models.LogLevel.Information => LogEventLevel.Information,
            Fidalgo.Agent.Models.LogLevel.Warning => LogEventLevel.Warning,
            Fidalgo.Agent.Models.LogLevel.Error => LogEventLevel.Error,
            Fidalgo.Agent.Models.LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }

    private static string SerializeExceptionDetails(ExceptionDetails details)
    {
        return System.Text.Json.JsonSerializer.Serialize(details, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
    }
}
