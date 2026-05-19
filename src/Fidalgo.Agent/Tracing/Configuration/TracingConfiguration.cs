namespace Fidalgo.Agent.Tracing.Configuration;

public class TracingConfiguration : ITracingConfiguration
{
    public Uri CollectorEndpoint { get; set; } = new Uri("http://localhost:4317");
    public bool IsEnabled { get; set; } = true;
    public int BufferSize { get; set; } = 1000;
    public TimeSpan ExportInterval { get; set; } = TimeSpan.FromSeconds(5);
}
