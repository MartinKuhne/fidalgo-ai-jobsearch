using Fidalgo.Agent.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Fidalgo.Agent.Logging;

public class TraceContextProvider : ITraceContextProvider
{
    private readonly AsyncLocal<TraceContext?> _currentContext = new();

    public TraceContext? GetCurrentContext()
    {
        return _currentContext.Value;
    }

    public void SetCurrentContext(TraceContext context)
    {
        _currentContext.Value = context;
    }
}
