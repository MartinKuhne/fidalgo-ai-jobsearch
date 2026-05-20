using System.Reflection;

namespace Fidalgo.Agent.Prompts;

public static class AgentPrompt
{
    private static string? _promptTemplate;
    private static readonly object _lock = new();

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

    public static string Generate(string email, string query, string resume, string location, string zipCode)
    {
        var prompt = GetPromptTemplate();

        prompt = prompt.Replace("{{email}}", email);
        prompt = prompt.Replace("{{resume}}", resume);
        prompt = prompt.Replace("{{query}}", query);
        prompt = prompt.Replace("{{location}}", location);
        prompt = prompt.Replace("{{zipCode}}", zipCode);

        return prompt;
    }
}
