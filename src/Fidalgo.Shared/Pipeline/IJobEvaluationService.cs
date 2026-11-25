using Fidalgo.Shared.Storage;

namespace Fidalgo.Shared.Pipeline;

/// <summary>
/// Evaluates a single job posting against a candidate's profile and updates its persisted scores.
/// </summary>
public interface IJobEvaluationService
{
    /// <summary>
    /// Evaluates <paramref name="job"/> against the supplied profile and saves the result.
    /// </summary>
    Task<int> EvaluateJobAsync(
        JobEntity job,
        string email,
        string resumeMarkdown,
        string? careerStoryMarkdown,
        string? careerGoalsMarkdown,
        CancellationToken cancellationToken = default);
}
