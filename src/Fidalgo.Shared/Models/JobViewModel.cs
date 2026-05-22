namespace Fidalgo.Shared.Models;

public record JobViewModel(
    Guid InternalId,
    string Email,
    string Employer,
    string? Title,
    int Score,
    string Recommendation,
    DateTime? PostedDate,
    string SourceWebsite);
