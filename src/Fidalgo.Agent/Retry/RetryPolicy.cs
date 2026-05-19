namespace Fidalgo.Agent.Retry;

public class RetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries = 3;
    private readonly TimeSpan _initialDelay = TimeSpan.FromMilliseconds(100);

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

    public bool IsTransient(Exception exception)
    {
        return exception is HttpRequestException or TimeoutException or IOException;
    }
}
