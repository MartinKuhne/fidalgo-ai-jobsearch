namespace Fidalgo.Shared.Retry;

/// <summary>
/// Retry policy with exponential backoff (3 retries, 100ms initial delay, 2x multiplier).
/// Detects transient failures from HttpRequestException, TimeoutException, and IOException.
/// </summary>
public class RetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries = 3;
    private readonly TimeSpan _initialDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>Executes an operation with retry on transient failures.</summary>
    public async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        var delay = _initialDelay;

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                if (attempt == _maxRetries)
                {
                    throw;
                }

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
        }

        throw new InvalidOperationException("Retry loop completed without returning a result");
    }

    /// <summary>Executes a void operation with retry on transient failures.</summary>
    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        var delay = _initialDelay;

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                if (attempt == _maxRetries)
                {
                    throw;
                }

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
        }
    }

    /// <summary>Determines if an exception is transient and worth retrying.</summary>
    public bool IsTransient(Exception exception)
    {
        return exception is HttpRequestException or TimeoutException or IOException;
    }
}