using System.Runtime.Serialization;

namespace Fidalgo.Shared.ErrorHandling;

/// <summary>
/// Custom exception for 401 Unauthorized scenarios.
/// Thrown when authentication is required and has failed or not been provided.
/// </summary>
public class UnauthorizedException : Exception
{
    /// <summary>Initializes a new instance with a default message.</summary>
    public UnauthorizedException() : base("Unauthorized") { }

    /// <summary>Initializes a new instance with a custom message.</summary>
    /// <param name="message">The error message text.</param>
    public UnauthorizedException(string message) : base(message) { }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message text.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public UnauthorizedException(string message, Exception innerException) : base(message, innerException) { }
}