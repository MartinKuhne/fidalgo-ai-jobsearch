using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.ErrorHandling;

/// <summary>
/// Maps specific exception types to HTTP status codes and user-friendly messages.
/// Handles validation errors, unauthorized access, not found, and generic server errors.
/// Provides structured extraction of field-level validation errors from ValidationException.
/// </summary>
public class ExceptionMapper : IExceptionMapper
{
    /// <summary>Maps an exception to its corresponding HTTP status code.</summary>
    /// <param name="exception">The exception to map.</param>
    /// <returns>HTTP status code (400, 401, 404, or 500).</returns>
    public int MapToStatusCode(Exception exception)
    {
        return exception switch
        {
            ValidationException => 400,
            UnauthorizedException => 401,
            NotFoundException => 404,
            _ => 500
        };
    }

    /// <summary>Maps an exception to a user-friendly error message.</summary>
    /// <param name="exception">The exception to map.</param>
    /// <returns>User-friendly message string.</returns>
    public string MapToUserMessage(Exception exception)
    {
        if (IsValidationError(exception))
        {
            return "Validation failed. Please check your input and try again.";
        }

        return "An unexpected error occurred. Please try again later.";
    }

    /// <summary>Checks if an exception is a validation error.</summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception is a ValidationException.</returns>
    public bool IsValidationError(Exception exception)
    {
        return exception is ValidationException;
    }

    /// <summary>Extracts field-level validation errors from a ValidationException.</summary>
    /// <param name="exception">The exception to extract errors from.</param>
    /// <returns>Dictionary of field names to error messages, or empty dictionary if not a validation error.</returns>
    public IDictionary<string, string> GetValidationErrors(Exception exception)
    {
        if (exception is ValidationException validationException)
        {
            return validationException.Errors
                .SelectMany(kvp => kvp.Value.Select(v => new { Key = kvp.Key, Value = v }))
                .ToDictionary(x => x.Key, x => x.Value);
        }

        return new Dictionary<string, string>();
    }
}