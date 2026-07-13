using System.Reflection;

using Fidalgo.Shared.Tools;
namespace Fidalgo.Ingest.Prompts;

/// <summary>
/// Loads and processes the AI agent prompt template from a bundled Markdown file.
/// Uses double-checked locking for thread-safe lazy loading of the template content.
/// Generates the final prompt by substituting email, resume, query, and zip code placeholders.
/// </summary>
public static class AgentPrompt
{
    private static string? _promptTemplate;
    private static readonly object _lock = new();

    /// <summary>Generates the final prompt by substituting placeholders in the template.</summary>
    /// <param name="email">The tenant's email address.</param>
    /// <param name="query">The job search query.</param>
    /// <param name="zipCode">The target zip code.</param>
    /// <returns>The prompt with all placeholders replaced.</returns>
    public static string Generate(string email, string query, string zipCode)
    {
        var prompt = GetPromptTemplate();

        prompt = prompt.Replace("{{email}}", email);
        prompt = prompt.Replace("{{query}}", query);
        prompt = prompt.Replace("{{zipCode}}", zipCode);

        return prompt;
    }

    private static string GetPromptTemplate()
    {
        if (_promptTemplate is not null)
        {
            return _promptTemplate;
        }

        lock (_lock)
        {
            if (_promptTemplate is not null)
                return _promptTemplate;

            _promptTemplate = LoadPromptTemplate();
            return _promptTemplate;
        }
    }

    private static string LoadPromptTemplate()
    {
        var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
        var promptPath = Path.Combine(executingAssemblyPath, "Prompts", "AgentPrompt.md");
        if (File.Exists(promptPath))
        {
            return File.ReadAllText(promptPath);
        }

        throw new FileNotFoundException("AgentPrompt.md not found at: " + promptPath);
    }
}