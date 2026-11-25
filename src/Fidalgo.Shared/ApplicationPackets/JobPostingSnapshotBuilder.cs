using System.Globalization;
using System.Text;
using Fidalgo.Shared.Storage;

namespace Fidalgo.Shared.ApplicationPackets;

/// <summary>
/// Builds the canonical <c>job-posting.md</c> Markdown snapshot persisted alongside generated PDFs.
/// </summary>
public static class JobPostingSnapshotBuilder
{
    /// <summary>
    /// Renders <paramref name="job"/> into a structured Markdown snapshot containing metadata,
    /// evaluation notes, and the full job description.
    /// </summary>
    public static string Build(JobEntity job)
    {
        ArgumentNullException.ThrowIfNull(job);

        var title = string.IsNullOrWhiteSpace(job.Title) ? "Untitled Role" : job.Title.Trim();
        var employer = string.IsNullOrWhiteSpace(job.Employer) ? "Unknown Employer" : job.Employer.Trim();
        var employerJobId = string.IsNullOrWhiteSpace(job.EmployerJobId) ? "N/A" : job.EmployerJobId.Trim();
        var sourceWebsite = string.IsNullOrWhiteSpace(job.SourceWebsite) ? "N/A" : job.SourceWebsite.Trim();
        var sourceUrl = JobUrlFormatter.FormatUrl(job);
        var postedDate = job.PostedDate.HasValue
            ? job.PostedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "N/A";
        var salaryRange = FormatSalaryRange(job.SalaryRangeLow, job.SalaryRangeHigh);
        var recommendation = string.IsNullOrWhiteSpace(job.Recommendation) ? "N/A" : job.Recommendation.Trim();

        var sb = new StringBuilder();
        sb.Append("# ").Append(title).Append(" — ").AppendLine(employer);
        sb.AppendLine();
        sb.AppendLine("## Job Metadata");
        sb.AppendLine();
        sb.Append("- **Internal ID:** `").Append(job.InternalId).AppendLine("`");
        sb.Append("- **Employer:** ").AppendLine(employer);
        sb.Append("- **Title:** ").AppendLine(title);
        sb.Append("- **Employer Job ID:** ").AppendLine(employerJobId);
        sb.Append("- **Source Website:** ").AppendLine(sourceWebsite);
        sb.Append("- **Source URL:** ").AppendLine(string.IsNullOrEmpty(sourceUrl) ? "N/A" : sourceUrl);
        sb.Append("- **Posted Date:** ").AppendLine(postedDate);
        sb.Append("- **Salary Range:** ").AppendLine(salaryRange);
        sb.Append("- **Match Score:** ").Append(job.Score.ToString(CultureInfo.InvariantCulture)).AppendLine("/100");
        sb.Append("- **Recommendation:** ").AppendLine(recommendation);
        sb.AppendLine();
        sb.AppendLine("## Evaluation Notes");
        sb.AppendLine();
        sb.AppendLine("### Pros");
        sb.AppendLine();
        sb.AppendLine(string.IsNullOrWhiteSpace(job.Pros) ? "_None recorded._" : job.Pros.Trim());
        sb.AppendLine();
        sb.AppendLine("### Cons");
        sb.AppendLine();
        sb.AppendLine(string.IsNullOrWhiteSpace(job.Cons) ? "_None recorded._" : job.Cons.Trim());
        sb.AppendLine();
        sb.AppendLine("### Resume Hints");
        sb.AppendLine();
        sb.AppendLine(string.IsNullOrWhiteSpace(job.ResumeHints) ? "_None recorded._" : job.ResumeHints.Trim());
        sb.AppendLine();
        sb.AppendLine("## Job Description");
        sb.AppendLine();
        sb.AppendLine(string.IsNullOrWhiteSpace(job.Description) ? "_No description provided._" : job.Description.Trim());

        return sb.ToString();
    }

    private static string FormatSalaryRange(decimal? low, decimal? high)
    {
        if (low.HasValue && high.HasValue && low.Value > 0 && high.Value > 0)
        {
            return $"${low.Value.ToString("N0", CultureInfo.InvariantCulture)} - ${high.Value.ToString("N0", CultureInfo.InvariantCulture)}";
        }

        if (low.HasValue && low.Value > 0)
        {
            return $"${low.Value.ToString("N0", CultureInfo.InvariantCulture)}";
        }

        if (high.HasValue && high.Value > 0)
        {
            return $"Up to ${high.Value.ToString("N0", CultureInfo.InvariantCulture)}";
        }

        return "Not specified";
    }
}
