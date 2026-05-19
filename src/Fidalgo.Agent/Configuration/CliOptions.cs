namespace Fidalgo.Agent.Configuration;

public record CliOptions
{
    public string Email { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string ResumePath { get; set; } = string.Empty;
    public string? NarrativePath { get; set; }
    public bool QueryJobs { get; set; }
    public string? EmployerFilter { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? SourceWebsiteFilter { get; set; }
    public Guid? DiscardJobId { get; set; }
    public bool ListDiscarded { get; set; }
}
