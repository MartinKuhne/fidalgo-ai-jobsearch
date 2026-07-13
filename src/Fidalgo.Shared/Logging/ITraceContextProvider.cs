using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.Logging;

/// <summary>
/// Contract for getting and setting the current trace context in the async execution flow.
/// Used to propagate trace identifiers across async boundaries.
/// </summary>
public interface ITraceContextProvider
{
    /// <summary>Returns the current trace context, or null if none is set.</summary>
    TraceContext? GetCurrentContext();

    /// <summary>Sets the current trace context for the async flow.</summary>
    void SetCurrentContext(TraceContext context);
}