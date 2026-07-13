namespace Fidalgo.Shared.Models;

/// <summary>
/// Immutable data transfer object representing a job for UI display.
/// Carries the essential job fields needed by the web frontend for rendering job listings.
/// Mapped from JobEntity by JobsService after database retrieval.
/// </summary>
public record JobViewModel(
    Guid InternalId,
    string Email,
    string Company,
    string? Title,
    int Score,
    string Recommendation,
    DateTime? PostedDate,
    string SourceWebsite,
    string Pay,
    string Description,
    string Url,
    string AiReasoning);