namespace Fidalgo.Shared.Models;

/// <summary>
/// Request parameters controlling packet generation overrides and idempotency.
/// </summary>
public sealed record ApplicationPacketGenerationRequest(
    UserProfileLoadOverrides? ProfileOverrides = null,
    bool ForceRegenerate = false,
    bool SkipExisting = true);
