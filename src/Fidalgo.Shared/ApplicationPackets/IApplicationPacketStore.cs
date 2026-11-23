using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;

namespace Fidalgo.Shared.ApplicationPackets;

/// <summary>
/// Persists, inspects, and streams per-job application packet artifacts.
/// </summary>
public interface IApplicationPacketStore
{
    /// <summary>
    /// Inspects the on-disk status of the application packet for <paramref name="job"/>.
    /// </summary>
    Task<ApplicationPacketStatus> GetStatusAsync(
        string baseOutputDir,
        JobEntity job,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically writes all packet artifacts (PDFs, Markdown sources, job posting, manifest) to disk.
    /// </summary>
    Task<ApplicationPacketFiles> SavePacketAsync(
        string baseOutputDir,
        JobEntity job,
        ApplicationPacketWriteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read-only asynchronous stream for an allowlisted packet file, or returns <see langword="null"/> if missing.
    /// </summary>
    Task<Stream?> OpenPacketFileAsync(
        string baseOutputDir,
        JobEntity job,
        string allowedFileName,
        CancellationToken cancellationToken = default);
}
