using System.Text;
using System.Text.RegularExpressions;

namespace Fidalgo.Shared.ApplicationPackets.Prompts;

/// <summary>
/// Shared prompt section formatting helper for tailoring prompts, handling XML delimiter sections
/// and neutralizing closing <c>&lt;/job_posting&gt;</c> tags inside untrusted job posting fields.
/// </summary>
internal static partial class PromptSectionWriter
{
    [GeneratedRegex(@"</\s*job_posting\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClosingJobPostingTagRegex();

    /// <summary>
    /// Appends a required or optional XML-delimited section when <paramref name="content"/> is non-whitespace.
    /// </summary>
    public static void AppendSectionIfPresent(
        StringBuilder sb,
        string tagName,
        string? content,
        bool leadingBlankLine = true)
    {
        ArgumentNullException.ThrowIfNull(sb);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        if (leadingBlankLine)
        {
            sb.AppendLine();
        }

        sb.Append('<').Append(tagName).AppendLine(">");
        sb.AppendLine(content.Trim());
        sb.Append("</").Append(tagName).AppendLine(">");
    }

    /// <summary>
    /// Appends the <c>&lt;job_posting&gt;</c> section while neutralizing any literal
    /// <c>&lt;/job_posting&gt;</c> tags inside untrusted job fields.
    /// </summary>
    public static void AppendJobPostingSection(
        StringBuilder sb,
        string employer,
        string? jobTitle,
        string? jobPros,
        string? resumeHints,
        string jobDescription)
    {
        ArgumentNullException.ThrowIfNull(sb);

        sb.AppendLine();
        sb.AppendLine("<job_posting>");
        sb.Append("Employer: ").AppendLine(
            string.IsNullOrWhiteSpace(employer) ? "Unknown Employer" : NeutralizeJobPostingBreakout(employer.Trim()));
        sb.Append("Title: ").AppendLine(
            string.IsNullOrWhiteSpace(jobTitle) ? "Untitled Role" : NeutralizeJobPostingBreakout(jobTitle.Trim()));

        if (!string.IsNullOrWhiteSpace(jobPros))
        {
            sb.AppendLine();
            sb.AppendLine("Evaluation Fit Highlights (Pros):");
            sb.AppendLine(NeutralizeJobPostingBreakout(jobPros.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(resumeHints))
        {
            sb.AppendLine();
            sb.AppendLine("Evaluation Resume Hints:");
            sb.AppendLine(NeutralizeJobPostingBreakout(resumeHints.Trim()));
        }

        sb.AppendLine();
        sb.AppendLine("Description:");
        sb.AppendLine(string.IsNullOrWhiteSpace(jobDescription)
            ? "No description provided."
            : NeutralizeJobPostingBreakout(jobDescription.Trim()));
        sb.AppendLine("</job_posting>");
    }

    /// <summary>
    /// Neutralizes case-insensitive closing <c>&lt;/job_posting&gt;</c> tags so untrusted job text
    /// cannot break out of the prompt delimiter.
    /// </summary>
    public static string NeutralizeJobPostingBreakout(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return ClosingJobPostingTagRegex().Replace(value, "&lt;/job_posting&gt;");
    }
}
