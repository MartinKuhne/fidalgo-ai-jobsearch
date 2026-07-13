using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.Tracing;

/// <summary>
/// Stub OTLP exporter implementation. ExportAsync and ShutdownAsync are no-ops.
/// To be replaced with a real implementation when an OTLP collector endpoint is available.
/// </summary>
public class OtlpExporter : IOtlpExporter
{
    /// <summary>Exports spans via OTLP (stub: no-op).</summary>
    public async Task ExportAsync(IReadOnlyList<Span> spans, CancellationToken cancellationToken = default)
    {
        // Export spans via OTLP
        await Task.CompletedTask;
    }

    /// <summary>Shuts down the exporter gracefully (stub: always returns true).</summary>
    public async Task<bool> ShutdownAsync(TimeSpan timeout)
    {
        await Task.CompletedTask;
        return true;
    }
}