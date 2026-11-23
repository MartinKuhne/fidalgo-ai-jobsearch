using System.Diagnostics;

namespace Fidalgo.Shared.Tracing;

/// <summary>
/// Registers an idempotent process-wide <see cref="ActivityListener"/> for all <c>Fidalgo.*</c>
/// <see cref="ActivitySource"/> instances so activities are created with valid W3C
/// <see cref="Activity.TraceId"/> and <see cref="Activity.SpanId"/> values even without an
/// external OpenTelemetry collector.
/// </summary>
public static class FidalgoTracingListener
{
    private static readonly ActivityListener Listener = new()
    {
        ShouldListenTo = source => source.Name.StartsWith("Fidalgo.", StringComparison.Ordinal),
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
    };

    private static int _registered;

    /// <summary>
    /// Ensures the <c>Fidalgo.*</c> <see cref="ActivityListener"/> is registered once per process.
    /// </summary>
    public static void EnsureRegistered()
    {
        if (Interlocked.CompareExchange(ref _registered, 1, 0) == 0)
        {
            ActivitySource.AddActivityListener(Listener);
        }
    }
}
