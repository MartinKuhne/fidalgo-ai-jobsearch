namespace Fidalgo.Agent.Retry;

public interface IRetryPolicy
{
    Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default);
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);
    bool IsTransient(Exception exception);
}
