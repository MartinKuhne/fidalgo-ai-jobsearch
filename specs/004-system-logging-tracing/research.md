# Research: System Logging and Tracing Infrastructure

**Feature**: 004-system-logging-tracing  
**Date**: 2026-05-19  
**Branch**: main

## Research Questions

This research addresses the following unknowns from the Technical Context section:

1. **Logging Framework**: How to configure structured logging with .NET's built-in logging framework for console and file output?
2. **OpenTelemetry Integration**: How to integrate OpenTelemetry SDK for distributed tracing and export to collector?
3. **Trace Context Propagation**: How to implement W3C Trace Context header propagation in .NET HTTP client/server?
4. **Exception Handling**: How to implement global exception handling with HTTP status code mapping in ASP.NET Core?
5. **Retry with Exponential Backoff**: What are the best practices for implementing retry logic with exponential backoff in .NET?
6. **Log File Rotation**: How to implement daily log file rotation and 7-day retention with .NET logging?

## Research Findings

### 1. Logging Framework Configuration

**Decision**: Use Microsoft.Extensions.Logging with Serilog for structured logging

**Rationale**: 
- Serilog provides first-class structured logging with JSON output
- Built-in .NET logging abstraction allows easy switching between providers
- Serilog.Sinks.Console and Serilog.Sinks.File provide console and file output
- Serilog.Sinks.RollingFile supports daily log file rotation

**Alternatives considered**:
- NLog: Powerful but more complex configuration
- Built-in console logger only: Lacks file output and structured JSON
- Log4Net: Older, less active community

**Implementation approach**:
```csharp
// Program.cs
builder.Host.UseSerilog((context, services, configuration) => {
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName);
});

// appsettings.json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.json",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "{TimestampUtc:O} {Level} {TraceId} {SpanId} {Message}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

### 2. OpenTelemetry Integration

**Decision**: Use OpenTelemetry .NET SDK with OTLP exporter

**Rationale**:
- Official .NET SDK from OpenTelemetry project
- W3C Trace Context support built-in
- OTLP (OpenTelemetry Protocol) is the standard export format
- Supports both metrics and traces

**Alternatives considered**:
- OpenTelemetry .NET Contrib: Community extensions, not needed for basic functionality
- Jaeger .NET Client: Vendor-specific, OTLP is more portable

**Implementation approach**:
```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder => {
        tracerProviderBuilder
            .AddSource("Fidalgo.Agent")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation()
            .AddOtlpExporter(otlpOptions => {
                otlpOptions.Endpoint = new Uri(builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317");
                otlpOptions.Protocol = OtlpExportProtocol.Grpc;
            });
    });
```

### 3. Trace Context Propagation

**Decision**: Use built-in .NET Activity API with W3C Trace Context

**Rationale**:
- .NET 6+ has built-in Activity API for distributed tracing
- Automatic W3C Trace Context header parsing and generation
- Compatible with OpenTelemetry SDK

**Implementation approach**:
```csharp
// Request processing (automatic with OpenTelemetry)
// Headers are automatically extracted and propagated

// For custom HTTP clients
public class TracingHttpClient : HttpClient
{
    private readonly ActivitySource _activitySource;
    
    public async Task<HttpResponseMessage> GetWithTracingAsync(string url, string traceId, string spanId)
    {
        var activity = new Activity("GetRequest")
            .SetParentId(traceId)
            .Start();
        
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("traceparent", activity.Id); // W3C format
            request.Headers.Add("tracestate", activity.TraceStateString);
            
            return await base.SendAsync(request);
        }
        finally
        {
            activity.Stop();
        }
    }
}
```

### 4. Global Exception Handling

**Decision**: Use ASP.NET Core middleware for global exception handling

**Rationale**:
- Middleware is the standard pattern for cross-cutting concerns
- Can catch all exceptions before response is sent
- Easy to integrate with logging framework

**Implementation approach**:
```csharp
// ExceptionHandlingMiddleware.cs
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }
    
    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.StatusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            NotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };
        
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            error = exception.Message,
            details = exception.StackTrace // Only in development
        };
        
        return context.Response.WriteAsJsonAsync(response);
    }
}
```

### 5. Retry with Exponential Backoff

**Decision**: Use Polly for retry policies

**Rationale**:
- Industry-standard resilience library for .NET
- Built-in exponential backoff policies
- Easy to configure and test
- Supports various fault types

**Alternatives considered**:
- Manual implementation: More code, harder to test
- Microsoft.Extensions.Resilience: New in .NET 9, Polly is more mature

**Implementation approach**:
```csharp
// RetryPolicy.cs
public static class RetryPolicy
{
    public static ResiliencePipeline<HttpResponseMessage> CreateRetryPolicy()
    {
        var options = new RetryStrategyOptions
        {
            Delay = TimeSpan.FromMilliseconds(100),
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true, // Add randomness to prevent thundering herd
            OnRetry = args =>
            {
                var logger = args.Context.GetValueOrDefault<ILogger>("Logger");
                logger?.LogWarning("Retry {Attempt} after {Delay}ms due to {Failure}", 
                    args.AttemptNumber, args.Delay.TotalMilliseconds, args.Failure);
                return ValueTask.CompletedTask;
            }
        };
        
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(options)
            .Build();
    }
}
```

### 6. Log File Rotation

**Decision**: Use Serilog.Sinks.RollingFile with daily interval

**Rationale**:
- Automatic daily rotation built-in
- Configurable retention (7 days)
- JSON output format supported
- No custom code needed

**Implementation approach**: See section 1 - already configured via Serilog configuration.

## Unknowns Resolved

| Unknown | Decision | Status |
|---------|----------|--------|
| Logging framework | Serilog with built-in .NET abstraction | ✅ Resolved |
| OpenTelemetry integration | OpenTelemetry .NET SDK + OTLP exporter | ✅ Resolved |
| Trace context propagation | .NET Activity API with W3C Trace Context | ✅ Resolved |
| Exception handling | ASP.NET Core middleware | ✅ Resolved |
| Retry with exponential backoff | Polly library | ✅ Resolved |
| Log file rotation | Serilog.Sinks.RollingFile with daily interval | ✅ Resolved |

## Dependencies Identified

1. **Serilog.AspNetCore** - ASP.NET Core integration for Serilog
2. **Serilog.Sinks.Console** - Console output
3. **Serilog.Sinks.File** - File output with JSON formatting
4. **OpenTelemetry.Exporter.OpenTelemetryProtocol** - OTLP exporter
5. **OpenTelemetry.Extensions.Hosting** - .NET hosting integration
6. **Polly** - Resilience and transient-fault-handling

## Next Steps

Proceed to Phase 1: Design & Contracts to create:
- `data-model.md` - Define TraceContext, LogEntry, Span entities
- `contracts/` - Define logging and tracing interfaces
- `quickstart.md` - Developer onboarding guide
