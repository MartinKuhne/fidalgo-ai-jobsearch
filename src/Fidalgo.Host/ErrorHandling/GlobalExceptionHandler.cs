using System.Diagnostics;
using Fidalgo.Shared.ErrorHandling;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Fidalgo.Host.ErrorHandling;

/// <summary>
/// Global ASP.NET Core exception handler implementing NFR-200 through NFR-203.
/// Maps domain and framework exceptions to sanitized ProblemDetails responses
/// without exposing stack traces to clients.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GlobalExceptionHandler"/>.</summary>
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        var instance = httpContext.Request.Path.Value;

        if (exception is ValidationException validationException)
        {
            _logger.LogWarning(
                validationException,
                "Validation failure for {Method} {Path} (TraceId={TraceId}).",
                httpContext.Request.Method,
                instance,
                traceId);

            var validationProblem = new ValidationProblemDetails(validationException.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Failed",
                Detail = validationException.Message,
                Instance = instance
            };
            validationProblem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(
                validationProblem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
            return true;
        }

        ProblemDetails problem = exception switch
        {
            BadHttpRequestException badHttpRequest => CreateProblemDetails(
                badHttpRequest.StatusCode,
                ReasonPhrases.GetReasonPhrase(badHttpRequest.StatusCode),
                badHttpRequest.Message,
                instance,
                traceId),
            NotFoundException notFound => CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Not Found",
                notFound.Message,
                instance,
                traceId),
            UnauthorizedException unauthorized => CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                unauthorized.Message,
                instance,
                traceId),
            AgentExecutionException => CreateProblemDetails(
                StatusCodes.Status502BadGateway,
                "Upstream Service Error",
                "An upstream AI or PDF rendering service failed to process the request.",
                instance,
                traceId),
            TimeoutException => CreateProblemDetails(
                StatusCodes.Status504GatewayTimeout,
                "Gateway Timeout",
                "The upstream operation timed out before completing.",
                instance,
                traceId),
            _ => CreateProblemDetails(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred. Reference the traceId when reporting this issue.",
                instance,
                traceId)
        };

        var statusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path} mapped to {StatusCode} (TraceId={TraceId}).",
                httpContext.Request.Method,
                instance,
                statusCode,
                traceId);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Request error for {Method} {Path} mapped to {StatusCode} (TraceId={TraceId}).",
                httpContext.Request.Method,
                instance,
                statusCode,
                traceId);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
        return true;
    }

    private static ProblemDetails CreateProblemDetails(
        int statusCode,
        string title,
        string detail,
        string? instance,
        string traceId)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = string.IsNullOrWhiteSpace(title) ? "Error" : title,
            Detail = detail,
            Instance = instance
        };
        problem.Extensions["traceId"] = traceId;
        return problem;
    }
}
