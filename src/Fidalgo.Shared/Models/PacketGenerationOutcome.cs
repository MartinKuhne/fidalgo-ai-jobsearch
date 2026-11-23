namespace Fidalgo.Shared.Models;

/// <summary>
/// Outcome of an application packet generation request.
/// </summary>
public enum PacketGenerationOutcome
{
    Generated,
    SkippedExisting
}
