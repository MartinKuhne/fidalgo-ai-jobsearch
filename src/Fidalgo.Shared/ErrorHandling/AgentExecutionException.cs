namespace Fidalgo.Shared.ErrorHandling;

/// <summary>
/// Exception thrown when the agent execution encounters an invalid state or unhandled finish reason.
/// </summary>
public class AgentExecutionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the AgentExecutionException class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public AgentExecutionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the AgentExecutionException class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public AgentExecutionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}