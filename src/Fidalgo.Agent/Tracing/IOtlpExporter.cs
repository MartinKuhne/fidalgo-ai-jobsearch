using Fidalgo.Agent.Models;

namespace Fidalgo.Agent.Tracing;

public interface IOtlpExporter
{
    Task ExportAsync(IReadOnlyList<Span> spans, CancellationToken cancellationToken = default);
    Task<bool> ShutdownAsync(TimeSpan timeout);
}
