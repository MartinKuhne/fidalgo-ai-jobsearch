namespace Fidalgo.Shared.Tracing.Configuration;

/// <summary>
/// Default tracing configuration with localhost OTLP endpoint.
/// Enabled by default, 1000 span buffer, 5-second export interval.
/// </summary>
public class TracingConfiguration : ITracingConfiguration
{
    /// <summary>OTLP collector endpoint URI (default: http://localhost:4317).</summary>
    public Uri CollectorEndpoint { get; set; } = new Uri("http://localhost:4317");

    /// <summary>Whether tracing is enabled (default: true).</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Maximum number of spans to buffer before export (default: 1000).</summary>
    public int BufferSize { get; set; } = 1000;

    /// <summary>Interval between automatic exports (default: 5 seconds).</summary>
    public TimeSpan ExportInterval { get; set; } = TimeSpan.FromSeconds(5);
}