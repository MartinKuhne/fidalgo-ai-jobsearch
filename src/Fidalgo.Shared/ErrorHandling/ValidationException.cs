using System.Runtime.Serialization;

namespace Fidalgo.Shared.ErrorHandling;

/// <summary>
/// Exception carrying field-level validation errors for 400 Bad Request scenarios.
/// Contains a dictionary mapping field names to arrays of error messages.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>Dictionary of field names to their validation error messages.</summary>
    public IDictionary<string, string[]> Errors { get; }

    /// <summary>Initializes a new instance with validation errors.</summary>
    /// <param name="errors">Dictionary mapping field names to error message arrays.</param>
    public ValidationException(IDictionary<string, string[]> errors)
        : base("Validation failed")
    {
        Errors = errors;
    }

    /// <summary>Initializes a new instance with a message and validation errors.</summary>
    /// <param name="message">The error message text.</param>
    /// <param name="errors">Dictionary mapping field names to error message arrays.</param>
    public ValidationException(string message, IDictionary<string, string[]> errors)
        : base(message)
    {
        Errors = errors;
    }

    /// <summary>Initializes a new instance with a message, inner exception, and validation errors.</summary>
    /// <param name="message">The error message text.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    /// <param name="errors">Dictionary mapping field names to error message arrays.</param>
    public ValidationException(string message, Exception innerException, IDictionary<string, string[]> errors)
        : base(message, innerException)
    {
        Errors = errors;
    }
}