using System.Text.Json;
using Fidalgo.Host.ErrorHandling;
using Fidalgo.Shared.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fidalgo.Host.Tests;

public class GlobalExceptionHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly GlobalExceptionHandler _handler = new(NullLogger<GlobalExceptionHandler>.Instance);

    [Fact]
    public async Task TryHandleAsync_BadHttpRequestException_PreservesStatusCode()
    {
        var context = CreateHttpContext("/api/jobs");
        var exception = new BadHttpRequestException("Required parameter \"email\" was not provided from query string.", StatusCodes.Status400BadRequest);

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var body = await ReadBodyAsync(context);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem!.Status);
        Assert.Contains("email", problem.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_Returns400WithFieldErrors()
    {
        var context = CreateHttpContext("/api/jobs/test/packet");
        var exception = new ValidationException(
            "Validation failed.",
            new Dictionary<string, string[]> { ["baseResume"] = ["Missing base resume."] });

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var body = await ReadBodyAsync(context);
        var problem = JsonSerializer.Deserialize<ValidationProblemDetails>(body, JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem!.Status);
        Assert.True(problem.Errors.ContainsKey("baseResume"));
        Assert.Contains("Missing base resume.", problem.Errors["baseResume"]);
    }

    [Fact]
    public async Task TryHandleAsync_NotFoundException_Returns404()
    {
        var context = CreateHttpContext("/api/jobs/123/packet");
        var handled = await _handler.TryHandleAsync(context, new NotFoundException("Job not found."), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_UnauthorizedException_Returns401()
    {
        var context = CreateHttpContext("/api/jobs");
        var handled = await _handler.TryHandleAsync(context, new UnauthorizedException("Invalid key."), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_AgentExecutionException_Returns502WithoutStackTrace()
    {
        var context = CreateHttpContext("/api/jobs/123/packet");
        var exception = new AgentExecutionException("Internal LLM secret URL http://192.168.1.21:8080/v1 failed");

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
        var body = await ReadBodyAsync(context);
        Assert.DoesNotContain("192.168.1.21", body);
        Assert.DoesNotContain("StackTrace", body);
    }

    [Fact]
    public async Task TryHandleAsync_TimeoutException_Returns504()
    {
        var context = CreateHttpContext("/api/jobs/123/packet");
        var handled = await _handler.TryHandleAsync(context, new TimeoutException("Timed out."), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status504GatewayTimeout, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_UnhandledException_Returns500WithTraceIdAndNoStackTrace()
    {
        var context = CreateHttpContext("/api/jobs");
        Exception thrown;
        try
        {
            throw new InvalidOperationException("Sensitive database connection detail");
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        var handled = await _handler.TryHandleAsync(context, thrown, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var body = await ReadBodyAsync(context);
        Assert.DoesNotContain("Sensitive database connection detail", body);
        Assert.DoesNotContain("GlobalExceptionHandlerTests", body);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem!.Extensions.ContainsKey("traceId"));
    }

    private static DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}
