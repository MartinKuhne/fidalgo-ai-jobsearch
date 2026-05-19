namespace Fidalgo.Agent.Storage;

public class JobEntity
{
    public Guid InternalId { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string Employer { get; set; } = string.Empty;
    public DateTime? PostedDate { get; set; }
    public string? EmployerJobId { get; set; }
    public decimal? SalaryRangeLow { get; set; }
    public decimal? SalaryRangeHigh { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Pros { get; set; } = string.Empty;
    public string Cons { get; set; } = string.Empty;
    public string ResumeHints { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DateNotified { get; set; }
    public string SourceWebsite { get; set; } = string.Empty;
}
