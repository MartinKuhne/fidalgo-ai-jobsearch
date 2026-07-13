namespace Fidalgo.Shared.Models;

/// <summary>
/// Immutable recursive structure representing exception details for logging.
/// Captures type, message, stack trace, inner exception, and source of an exception.
/// </summary>
public record ExceptionDetails(
    string Type,
    string Message,
    string StackTrace,
    ExceptionDetails? InnerException,
    string? Source);