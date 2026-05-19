using System.Runtime.Serialization;

namespace Fidalgo.Agent.ErrorHandling;

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("Validation failed")
    {
        Errors = errors;
    }

    public ValidationException(string message, IDictionary<string, string[]> errors)
        : base(message)
    {
        Errors = errors;
    }

    public ValidationException(string message, Exception innerException, IDictionary<string, string[]> errors)
        : base(message, innerException)
    {
        Errors = errors;
    }
}
