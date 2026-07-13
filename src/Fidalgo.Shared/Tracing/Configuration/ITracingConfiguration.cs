namespace Fidalgo.Shared.Tracing.Configuration;

/// <summary>
/// Configuration contract for tracing infrastructure settings.
/// Controls OTLP collector endpoint, enabled flag, buffer size, and export interval.
/// </summary>
public interface ITracingConfiguration
{
    /// <summary>OTLP collector endpoint URI.</summary>
    Uri CollectorEndpoint { get; }

    /// <summary>Whether tracing is enabled.</summary>
    bool IsEnabled { get; }

    /// <summary>Maximum number of spans to buffer before export.</summary>
    int BufferSize { get; }

    /// <summary>Interval between automatic exports.</summary>
    TimeSpan ExportInterval { get; }
}