using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.Pipeline;

/// <summary>
/// Runs one full ingest -> evaluate -> close-fit packet-generation cycle.
/// </summary>
public interface IJobPipelineRunner
{
    /// <summary>Executes one pipeline cycle and returns summary counts.</summary>
    Task<PipelineCycleResult> RunCycleAsync(CancellationToken cancellationToken = default);
}
