using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.Models;
using Microsoft.Extensions.Options;

namespace Fidalgo.Host.Services;

/// <summary>
/// Thread-safe state tracker exposing the latest execution status of <see cref="JobPipelineBackgroundService"/>.
/// </summary>
public class PipelineStatusTracker
{
    private readonly object _sync = new();
    private readonly IOptions<PipelineOptions> _options;
    private bool _isRunning;
    private DateTime? _lastCycleStartedAtUtc;
    private DateTime? _lastCycleCompletedAtUtc;
    private PipelineCycleResult? _lastResult;
    private string? _lastError;

    /// <summary>Initializes a new instance of <see cref="PipelineStatusTracker"/>.</summary>
    public PipelineStatusTracker(IOptions<PipelineOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>Marks a pipeline cycle as started.</summary>
    public void MarkStarted(DateTime utcNow)
    {
        lock (_sync)
        {
            _isRunning = true;
            _lastCycleStartedAtUtc = utcNow;
            _lastError = null;
        }
    }

    /// <summary>Marks the current cycle as completed with <paramref name="result"/>.</summary>
    public void MarkCompleted(PipelineCycleResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        lock (_sync)
        {
            _isRunning = false;
            _lastCycleCompletedAtUtc = result.CompletedAtUtc;
            _lastResult = result;
            _lastError = null;
        }
    }

    /// <summary>Marks the current cycle as failed with <paramref name="error"/>.</summary>
    public void MarkFailed(string error, DateTime utcNow)
    {
        lock (_sync)
        {
            _isRunning = false;
            _lastCycleCompletedAtUtc = utcNow;
            _lastError = error;
        }
    }

    /// <summary>Returns an immutable snapshot of the pipeline status.</summary>
    public PipelineStatusSnapshot GetSnapshot()
    {
        var opts = _options.Value;
        lock (_sync)
        {
            return new PipelineStatusSnapshot(
                Enabled: opts.Enabled,
                IntervalHours: opts.IntervalHours,
                MinScore: opts.MinScore,
                RecommendationFilter: opts.RecommendationFilter,
                IsRunning: _isRunning,
                LastCycleStartedAtUtc: _lastCycleStartedAtUtc,
                LastCycleCompletedAtUtc: _lastCycleCompletedAtUtc,
                LastResult: _lastResult,
                LastError: _lastError);
        }
    }
}
