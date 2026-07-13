using Fidalgo.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Fidalgo.Shared.Logging;

/// <summary>
/// Stores trace context per async execution flow using AsyncLocal.
/// Enables trace identifier propagation across async method boundaries.
/// </summary>
public class TraceContextProvider : ITraceContextProvider
{
    private readonly AsyncLocal<TraceContext?> _currentContext = new();

    /// <summary>Returns the current trace context, or null if none is set.</summary>
    public TraceContext? GetCurrentContext()
    {
        return _currentContext.Value;
    }

    /// <summary>Sets the current trace context for the async flow.</summary>
    public void SetCurrentContext(TraceContext context)
    {
        _currentContext.Value = context;
    }
}