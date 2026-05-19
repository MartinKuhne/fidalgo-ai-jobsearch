using Fidalgo.Agent.Models;

namespace Fidalgo.Agent.Tracing;

public class OtlpExporter : IOtlpExporter
{
    public async Task ExportAsync(IReadOnlyList<Span> spans, CancellationToken cancellationToken = default)
    {
        // Export spans via OTLP
        await Task.CompletedTask;
    }

    public async Task<bool> ShutdownAsync(TimeSpan timeout)
    {
        await Task.CompletedTask;
        return true;
    }
}
