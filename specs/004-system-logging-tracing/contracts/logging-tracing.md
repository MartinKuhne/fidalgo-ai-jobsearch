# Logging and Tracing Contracts

**Feature**: 004-system-logging-tracing  
**Date**: 2026-05-19  
**Branch**: main

## Overview

This document defines the interfaces and contracts for the logging and tracing infrastructure. These contracts follow the Dependency Inversion Principle - implementations depend on abstractions, not concrete types.

## ITraceContextProvider

Provides access to the current trace context for the active request.

```csharp
public interface ITraceContextProvider
{
    /// <summary>
    /// Gets the current trace context for the active request.
    /// Returns null if no trace context is available (e.g., outside request scope).
    /// </summary>
    TraceContext? GetCurrentContext();
    
    /// <summary>
    /// Sets the trace context for the active request.
    /// Should only be called once per request (at the start).
    /// </summary>
    /// <param name="context">The trace context to set.</param>
    void SetCurrentContext(TraceContext context);
}
```

## ILogEntryWriter

Writes log entries to the configured outputs (console, file, OpenTelemetry).

```csharp
public interface ILogEntryWriter
{
    /// <summary>
    /// Writes a log entry to all configured outputs.
    /// </summary>
    /// <param name="logEntry">The log entry to write.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Task representing the async operation.</returns>
    Task WriteAsync(LogEntry logEntry, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Writes a log entry with the specified level.
    /// </summary>
    /// <param name="level">The log level.</param>
    /// <param name="message">The log message.</param>
    /// <param name="traceId">The trace ID for correlation.</param>
    /// <param name="spanId">The span ID for correlation.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="exception">Optional exception details.</param>
    /// <param name="properties">Optional additional properties.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Task representing the async operation.</returns>
    Task WriteAsync(
        LogLevel level,
        string message,
        string traceId,
        string spanId,
        string correlationId,
        ExceptionDetails? exception = null,
        IDictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Writes an error-level log entry with exception details.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="message">The log message.</param>
    /// <param name="traceId">The trace ID for correlation.</param>
    /// <param name="spanId">The span ID for correlation.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="properties">Optional additional properties.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Task representing the async operation.</returns>
    Task WriteErrorAsync(
        Exception exception,
        string message,
        string traceId,
        string spanId,
        string correlationId,
        IDictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default);
}
```

## ISpanFactory

Creates and manages spans for distributed tracing.

```csharp
public interface ISpanFactory
{
    /// <summary>
    /// Creates a new root span for the current request.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="traceContext">The trace context for the request.</param>
    /// <returns>The created span.</returns>
    Span CreateRootSpan(string operationName, TraceContext traceContext);
    
    /// <summary>
    /// Creates a new child span for an operation within the current trace.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="parentSpan">The parent span.</param>
    /// <returns>The created child span.</returns>
    Span CreateChildSpan(string operationName, Span parentSpan);
    
    /// <summary>
    /// Starts the span and records the start time.
    /// </summary>
    /// <param name="span">The span to start.</param>
    void Start(Span span);
    
    /// <summary>
    /// Stops the span and records the end time.
    /// </summary>
    /// <param name="span">The span to stop.</param>
    /// <param name="status">The final status of the span.</param>
    void Stop(Span span, SpanStatus status = SpanStatus.Ok);
    
    /// <summary>
    /// Adds an event to the span.
    /// </summary>
    /// <param name="span">The span.</param>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="attributes">Optional event attributes.</param>
    void AddEvent(Span span, string eventName, IDictionary<string, object>? attributes = null);
    
    /// <summary>
    /// Adds an attribute to the span.
    /// </summary>
    /// <param name="span">The span.</param>
    /// <param name="key">The attribute key.</param>
    /// <param name="value">The attribute value.</param>
    void AddAttribute(Span span, string key, object value);
}
```

## IExceptionMapper

Maps exceptions to HTTP status codes and user-facing error messages.

```csharp
public interface IExceptionMapper
{
    /// <summary>
    /// Maps an exception to an HTTP status code.
    /// </summary>
    /// <param name="exception">The exception to map.</param>
    /// <returns>The HTTP status code.</returns>
    int MapToStatusCode(Exception exception);
    
    /// <summary>
    /// Maps an exception to a user-facing error message.
    /// This message should be safe to show to end users.
    /// </summary>
    /// <param name="exception">The exception to map.</param>
    /// <returns>The user-facing error message.</returns>
    string MapToUserMessage(Exception exception);
    
    /// <summary>
    /// Determines if the exception is a validation error.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception represents a validation error.</returns>
    bool IsValidationError(Exception exception);
    
    /// <summary>
    /// Extracts validation error details from the exception.
    /// </summary>
    /// <param name="exception">The exception to extract from.</param>
    /// <returns>Dictionary of field names to error messages.</returns>
    IDictionary<string, string> GetValidationErrors(Exception exception);
}
```

## IRetryPolicy

Defines the retry policy for transient failures.

```csharp
public interface IRetryPolicy
{
    /// <summary>
    /// Executes an operation with retry logic.
    /// </summary>
    /// <typeparam name="TResult">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="Exception">The last exception if all retries fail.</exception>
    Task<TResult> ExecuteAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes an operation with retry logic.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <exception cref="Exception">The last exception if all retries fail.</exception>
    Task ExecuteAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Determines if an exception is considered transient and should trigger a retry.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception is transient.</returns>
    bool IsTransient(Exception exception);
}
```

## ITraceContextPropagator

Propagates trace context to downstream services.

```csharp
public interface ITraceContextPropagator
{
    /// <summary>
    /// Propagates trace context to an HTTP request.
    /// </summary>
    /// <param name="request">The HTTP request to modify.</param>
    /// <param name="traceContext">The trace context to propagate.</param>
    void PropagateToHttpRequest(HttpRequestMessage request, TraceContext traceContext);
    
    /// <summary>
    /// Extracts trace context from an HTTP request.
    /// </summary>
    /// <param name="request">The HTTP request to extract from.</param>
    /// <returns>The extracted trace context, or null if not found.</returns>
    TraceContext? ExtractFromHttpRequest(HttpRequest request);
    
    /// <summary>
    /// Creates a new trace context from incoming headers.
    /// </summary>
    /// <param name="traceId">The incoming trace ID.</param>
    /// <param name="spanId">The incoming span ID.</param>
    /// <param name="correlationId">The incoming correlation ID.</param>
    /// <returns>The created trace context.</returns>
    TraceContext CreateFromHeaders(string? traceId, string? spanId, string? correlationId);
}
```

## OpenTelemetry Export Contracts

### IOtlpExporter

Exports trace data to an OpenTelemetry collector via OTLP.

```csharp
public interface IOtlpExporter
{
    /// <summary>
    /// Exports a batch of spans to the OpenTelemetry collector.
    /// </summary>
    /// <param name="spans">The spans to export.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Task representing the async operation.</returns>
    Task ExportAsync(IReadOnlyList<SpanData> spans, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Shuts down the exporter and flushes any pending data.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for flush.</param>
    /// <returns>True if flush succeeded, false if timeout occurred.</returns>
    Task<bool> ShutdownAsync(TimeSpan timeout);
}
```

## Configuration Contracts

### ILoggingConfiguration

Configuration for logging behavior.

```csharp
public interface ILoggingConfiguration
{
    /// <summary>
    /// Gets the minimum log level to output.
    /// </summary>
    LogLevel MinimumLevel { get; }
    
    /// <summary>
    /// Gets the path to the log file directory.
    /// </summary>
    string LogFilePath { get; }
    
    /// <summary>
    /// Gets the number of days to retain log files.
    /// </summary>
    int RetentionDays { get; }
    
    /// <summary>
    /// Gets a value indicating whether to write to console.
    /// </summary>
    bool WriteToConsole { get; }
    
    /// <summary>
    /// Gets a value indicating whether to write to file.
    /// </summary>
    bool WriteToFile { get; }
}
```

### ITracingConfiguration

Configuration for tracing behavior.

```csharp
public interface ITracingConfiguration
{
    /// <summary>
    /// Gets the OpenTelemetry collector endpoint.
    /// </summary>
    Uri CollectorEndpoint { get; }
    
    /// <summary>
    /// Gets a value indicating whether tracing is enabled.
    /// </summary>
    bool IsEnabled { get; }
    
    /// <summary>
    /// Gets the maximum number of spans to buffer before exporting.
    /// </summary>
    int BufferSize { get; }
    
    /// <summary>
    /// Gets the interval between batch exports.
    /// </summary>
    TimeSpan ExportInterval { get; }
}
```

## Error Response Contracts

### ErrorResponse

Standard error response format for API errors.

```csharp
public class ErrorResponse
{
    /// <summary>
    /// The HTTP status code.
    /// </summary>
    public int StatusCode { get; set; }
    
    /// <summary>
    /// A human-readable error message (safe for end users).
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Error details (only included in development).
    /// </summary>
    public string? Details { get; set; }
    
    /// <summary>
    /// Validation error details (field-level errors).
    /// </summary>
    public IDictionary<string, string>? ValidationErrors { get; set; }
    
    /// <summary>
    /// The trace ID for correlating this error with logs.
    /// </summary>
    public string TraceId { get; set; } = string.Empty;
}
```

### ValidationProblemDetails

Detailed validation error information.

```csharp
public class ValidationProblemDetails
{
    /// <summary>
    /// Dictionary of field names to error messages.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; set; } = new Dictionary<string, string[]>();
    
    /// <summary>
    /// The HTTP status code (typically 400).
    /// </summary>
    public int Status { get; set; }
    
    /// <summary>
    /// A title describing the error type.
    /// </summary>
    public string? Title { get; set; }
    
    /// <summary>
    /// The trace ID for correlation.
    /// </summary>
    public string TraceId { get; set; } = string.Empty;
}
```

## Usage Examples

### Logging

```csharp
public class MyService
{
    private readonly ILogEntryWriter _logWriter;
    private readonly ITraceContextProvider _traceContext;
    
    public async Task DoWorkAsync()
    {
        var context = _traceContext.GetCurrentContext();
        if (context == null)
            throw new InvalidOperationException("No trace context available");
        
        await _logWriter.WriteAsync(
            LogLevel.Information,
            "Starting work",
            context.TraceId,
            context.SpanId,
            context.CorrelationId);
        
        try
        {
            // Work here
        }
        catch (Exception ex)
        {
            await _logWriter.WriteErrorAsync(
                ex,
                "Work failed",
                context.TraceId,
                context.SpanId,
                context.CorrelationId);
            throw;
        }
    }
}
```

### Tracing

```csharp
public class MyController : ControllerBase
{
    private readonly ISpanFactory _spanFactory;
    private readonly ITraceContextProvider _traceContext;
    
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        var context = _traceContext.GetCurrentContext();
        using var span = _spanFactory.CreateChildSpan("ControllerGet", context!.SpanId);
        _spanFactory.Start(span);
        
        try
        {
            // Controller logic
            _spanFactory.AddAttribute(span, "controller", "MyController");
            _spanFactory.AddAttribute(span, "action", "Get");
            
            return Ok();
        }
        catch (Exception ex)
        {
            _spanFactory.Stop(span, SpanStatus.Error);
            throw;
        }
    }
}
```
