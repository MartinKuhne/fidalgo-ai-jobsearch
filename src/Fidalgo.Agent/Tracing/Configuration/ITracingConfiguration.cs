namespace Fidalgo.Agent.Tracing.Configuration;

public interface ITracingConfiguration
{
    Uri CollectorEndpoint { get; }
    bool IsEnabled { get; }
    int BufferSize { get; }
    TimeSpan ExportInterval { get; }
}
