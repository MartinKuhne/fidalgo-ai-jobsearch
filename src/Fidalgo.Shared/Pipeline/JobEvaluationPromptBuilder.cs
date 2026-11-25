using Fidalgo.Shared.Storage;

namespace Fidalgo.Shared.Pipeline;

/// <summary>
/// Pure static builder for the job evaluation system prompt consumed by both
/// <c>JobEvaluationAgent</c> and <see cref="LlmJobEvaluationService"/>.
/// </summary>
public static class JobEvaluationPromptBuilder
{
    private const int MaxResumeChars = 60000;

    /// <summary>
    /// Builds the evaluation system prompt for <paramref name="job"/> given candidate profile inputs.
    /// </summary>
    public static string BuildPrompt(
        string email,
        string resume,
        string? careerStory,
        string? careerGoals,
        JobEntity job)
    {
        ArgumentNullException.ThrowIfNull(job);

        var truncatedResume = TruncateResume(resume ?? string.Empty);
        var careerStorySection = BuildOptionalSection("Career Story", careerStory);
        var careerGoalsSection = BuildOptionalSection("Career Goals", careerGoals);

        return $@"You are an AI job evaluation assistant. Your task is to evaluate the following job posting against the user's resume.
User Email: {email}
Resume:
{truncatedResume}
{careerStorySection}{careerGoalsSection}
Job Title: {job.Title}
Employer: {job.Employer}
Job Description:
{job.Description}

Instructions:
1. Extract the required skills, preferred skills, pay, and location details from the job description.
2. Compare those details against the user's resume, career story, and career goals.
3. Determine a match score (0-100).
4. Determine the Pros (why it's a good fit) and Cons (potential concerns).
5. Generate Resume Hints (tailoring recommendations).
6. Provide a Recommendation. Must be EXACTLY ""Apply"", ""Maybe"", or ""Do not apply"".
7. Use the update_job tool to save your evaluation. DO NOT output formatted text, only call the tool.";
    }

    private static string TruncateResume(string resume) =>
        resume.Length <= MaxResumeChars ? resume : resume[..MaxResumeChars];

    private static string BuildOptionalSection(string label, string? content) =>
        string.IsNullOrWhiteSpace(content) ? string.Empty : $"{label}:\n{content}\n\n";
}
