using System.Runtime.Serialization;

namespace Fidalgo.Shared.ErrorHandling;

/// <summary>
/// Custom exception for 404 Not Found scenarios.
/// Thrown when a requested resource does not exist in the system.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>Initializes a new instance with a default message.</summary>
    public NotFoundException() : base("Not found") { }

    /// <summary>Initializes a new instance with a custom message.</summary>
    /// <param name="message">The error message text.</param>
    public NotFoundException(string message) : base(message) { }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message text.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }
}