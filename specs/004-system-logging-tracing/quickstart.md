# Quickstart: Logging and Tracing Infrastructure

**Feature**: 004-system-logging-tracing  
**Date**: 2026-05-19  
**Branch**: main

## Overview

This quickstart guide helps developers get started with the logging and tracing infrastructure. It covers basic usage patterns and common scenarios.

## Prerequisites

- .NET 10.0 SDK installed
- Basic understanding of ASP.NET Core
- Familiarity with dependency injection

## Setting Up Logging

### 1. Configure Logging in Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add logging services
builder.Services.AddLogging(loggingBuilder =>
{
    // Configure logging
    loggingBuilder.AddConfiguration(builder.Configuration.GetSection("Logging"));
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
    loggingBuilder.AddEventSourceLogger();
});

var app = builder.Build();

// Use logging middleware
app.UseLogging();

app.Run();
```

### 2. Configure Log Levels

Add to `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    },
    "Console": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  }
}
```

### 3. Inject ILogger into Services

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;
    
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }
    
    public async Task DoWorkAsync()
    {
        _logger.LogInformation("Starting work");
        
        try
        {
            // Work here
            _logger.LogInformation("Work completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Work failed");
            throw;
        }
    }
}
```

## Setting Up Tracing

### 1. Configure OpenTelemetry

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("Fidalgo.Agent")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation()
            .AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317");
            });
    });

var app = builder.Build();

app.Run();
```

### 2. Configure OpenTelemetry Settings

Add to `appsettings.json`:

```json
{
  "OpenTelemetry": {
    "Endpoint": "http://localhost:4317",
    "Enabled": true
  }
}
```

### 3. Use ActivitySource for Custom Spans

```csharp
public class MyService
{
    private readonly ActivitySource _activitySource;
    
    public MyService(ActivitySource activitySource)
    {
        _activitySource = activitySource;
    }
    
    public async Task DoWorkAsync()
    {
        using var activity = _activitySource.StartActivity("DoWork");
        activity?.SetTag("service", "MyService");
        
        try
        {
            // Work here
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

## Exception Handling

### 1. Use Built-in Exception Handling Middleware

```csharp
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Add exception handling middleware
app.UseExceptionHandler("/error");
app.UseHsts();

app.Run();
```

### 2. Create Custom Exception Handler

```csharp
public class CustomExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CustomExceptionHandlingMiddleware> _logger;
    
    public CustomExceptionHandlingMiddleware(
        RequestDelegate next, 
        ILogger<CustomExceptionHandlingMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception");
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
            error = exception.Message
        };
        
        return context.Response.WriteAsJsonAsync(response);
    }
}
```

### 3. Register Custom Exception Handler

```csharp
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseMiddleware<CustomExceptionHandlingMiddleware>();

app.Run();
```

## Retry with Exponential Backoff

### 1. Configure Retry Policy

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("RetryPolicy")
    .AddPolicyHandler(GetRetryPolicy());

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return Policy<HttpResponseMessage>
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == HttpStatusCode.ServiceUnavailable)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: (attempt) => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                var logger = context.Context["Logger"] as ILogger;
                logger?.LogWarning("Retry {RetryCount} after {Delay}", retryCount, timespan);
            });
}

var app = builder.Build();

app.Run();
```

### 2. Use Retry Policy

```csharp
public class MyService
{
    private readonly HttpClient _httpClient;
    
    public MyService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<string> GetDataAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
```

## Testing

### 1. Unit Test with Mocked Logger

```csharp
public class MyServiceTests
{
    [Fact]
    public async Task DoWorkAsync_LogsInformation()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<MyService>>();
        var service = new MyService(loggerMock.Object);
        
        // Act
        await service.DoWorkAsync();
        
        // Assert
        loggerMock.Verify(
            logger => logger.Log(
                It.Is<LogLevel>(l => l == LogLevel.Information),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}
```

### 2. Integration Test with Logging

```csharp
public class LoggingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    
    public LoggingIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }
    
    [Fact]
    public async Task Get_EndpointLogsRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/api/values");
        
        // Assert
        response.EnsureSuccessStatusCode();
        // Verify log entries were written
    }
}
```

## Common Scenarios

### Scenario 1: Logging in a Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class ValuesController : ControllerBase
{
    private readonly ILogger<ValuesController> _logger;
    
    public ValuesController(ILogger<ValuesController> logger)
    {
        _logger = logger;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        _logger.LogInformation("Received GET request");
        
        try
        {
            // Process request
            return Ok(new { message = "Success" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request failed");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
```

### Scenario 2: Tracing Across Service Boundaries

```csharp
public class ExternalServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ActivitySource _activitySource;
    
    public ExternalServiceClient(HttpClient httpClient, ActivitySource activitySource)
    {
        _httpClient = httpClient;
        _activitySource = activitySource;
    }
    
    public async Task<string> CallExternalServiceAsync(string url)
    {
        using var activity = _activitySource.StartActivity("CallExternalService");
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        
        // Propagate trace context
        if (activity != null)
        {
            request.Headers.Add("traceparent", activity.Id);
        }
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync();
    }
}
```

### Scenario 3: Handling Validation Errors

```csharp
public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }
    
    public ValidationException(IDictionary<string, string[]> errors)
        : base("Validation failed")
    {
        Errors = errors;
    }
}

public class MyController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromBody] MyModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors?.Count > 0)
                .ToDictionary(
                    x => x.Key,
                    x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
            
            throw new ValidationException(errors);
        }
        
        // Process valid model
        return Ok();
    }
}
```

## Best Practices

1. **Use structured logging**: Include relevant properties as separate parameters
2. **Include trace IDs**: Always include trace and span IDs in log messages
3. **Use appropriate log levels**: ERROR for failures, WARN for degraded states, INFO for key events
4. **Avoid sensitive data**: Never log passwords, tokens, or personal information
5. **Use correlation IDs**: Include user-facing IDs for easy tracking
6. **Keep spans short**: Only measure the operation you're interested in
7. **Handle exceptions gracefully**: Don't let exceptions break your logging code
8. **Test logging**: Verify log entries are written as expected

## Troubleshooting

### Logs not appearing?

- Check log level configuration
- Verify logging providers are registered
- Check console output for errors

### Traces not showing in collector?

- Verify OpenTelemetry endpoint configuration
- Check network connectivity to collector
- Ensure tracing is enabled in configuration

### High memory usage?

- Reduce log verbosity
- Increase log rotation frequency
- Consider async logging for high-throughput scenarios
