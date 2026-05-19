using Fidalgo.Agent.Models;

namespace Fidalgo.Agent.ErrorHandling;

public class ExceptionMapper : IExceptionMapper
{
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

    public string MapToUserMessage(Exception exception)
    {
        if (IsValidationError(exception))
        {
            return "Validation failed. Please check your input and try again.";
        }

        return "An unexpected error occurred. Please try again later.";
    }

    public bool IsValidationError(Exception exception)
    {
        return exception is ValidationException;
    }

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
