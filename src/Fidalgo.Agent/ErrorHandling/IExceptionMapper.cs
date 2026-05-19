using Fidalgo.Agent.Models;

namespace Fidalgo.Agent.ErrorHandling;

public interface IExceptionMapper
{
    int MapToStatusCode(Exception exception);
    string MapToUserMessage(Exception exception);
    bool IsValidationError(Exception exception);
    IDictionary<string, string> GetValidationErrors(Exception exception);
}
