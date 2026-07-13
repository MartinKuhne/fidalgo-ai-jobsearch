namespace Fidalgo.Shared.Retry;

/// <summary>
/// Contract for retry operations with exponential backoff on transient failures.
/// Provides both result-returning and void variants for different operation types.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>Executes an operation with retry on transient failures.</summary>
    Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default);

    /// <summary>Executes a void operation with retry on transient failures.</summary>
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);

    /// <summary>Determines if an exception is transient and worth retrying.</summary>
    bool IsTransient(Exception exception);
}