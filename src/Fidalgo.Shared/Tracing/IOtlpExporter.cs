using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.Tracing;

/// <summary>
/// Contract for exporting tracing spans via the OTLP (OpenTelemetry Protocol).
/// </summary>
public interface IOtlpExporter
{
    /// <summary>Exports a batch of spans via OTLP.</summary>
    Task ExportAsync(IReadOnlyList<Span> spans, CancellationToken cancellationToken = default);

    /// <summary>Shuts down the exporter gracefully.</summary>
    Task<bool> ShutdownAsync(TimeSpan timeout);
}