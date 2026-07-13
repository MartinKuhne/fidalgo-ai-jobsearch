using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.ErrorHandling;

/// <summary>
/// Contract for mapping technical exceptions to HTTP status codes and user-friendly messages.
/// Used by error handling middleware to produce appropriate HTTP responses.
/// </summary>
public interface IExceptionMapper
{
    /// <summary>Maps an exception to its corresponding HTTP status code.</summary>
    int MapToStatusCode(Exception exception);

    /// <summary>Maps an exception to a user-friendly error message.</summary>
    string MapToUserMessage(Exception exception);

    /// <summary>Checks if an exception is a validation error.</summary>
    bool IsValidationError(Exception exception);

    /// <summary>Extracts field-level validation errors from a ValidationException.</summary>
    IDictionary<string, string> GetValidationErrors(Exception exception);
}