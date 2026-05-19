using Fidalgo.Agent.Models;

namespace Fidalgo.Agent.Logging;

public interface ITraceContextProvider
{
    TraceContext? GetCurrentContext();
    void SetCurrentContext(TraceContext context);
}
