namespace Fidalgo.Shared.Pipeline;

/// <summary>
/// Periodically or on-demand ingests job postings for a tenant into local storage.
/// </summary>
public interface IJobIngestionService
{
    /// <summary>
    /// Searches and saves newly discovered jobs for <paramref name="email"/> and returns the count saved.
    /// </summary>
    Task<int> IngestAsync(
        string email,
        string keywords,
        string zipCode,
        CancellationToken cancellationToken = default);
}
