using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;

namespace Fidalgo.Shared.ApplicationPackets;

/// <summary>
/// Orchestrates end-to-end application packet generation (tailored resume PDF, cover letter PDF,
/// and job posting snapshot) with idempotency support.
/// </summary>
public interface IApplicationPacketService
{
    /// <summary>
    /// Generates or returns an existing application packet for the specified job ID.
    /// </summary>
    Task<ApplicationPacketResult> GenerateForJobAsync(
        Guid jobId,
        ApplicationPacketGenerationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates or returns an existing application packet for the supplied <paramref name="job"/>.
    /// </summary>
    Task<ApplicationPacketResult> GenerateForJobAsync(
        JobEntity job,
        ApplicationPacketGenerationRequest request,
        CancellationToken cancellationToken = default);
}
