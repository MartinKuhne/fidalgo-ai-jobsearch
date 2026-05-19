namespace Fidalgo.Agent.Models;

public record ExceptionDetails(
    string Type,
    string Message,
    string StackTrace,
    ExceptionDetails? InnerException,
    string? Source);
