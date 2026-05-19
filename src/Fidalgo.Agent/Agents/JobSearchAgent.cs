using OpenAI.Chat;
using Fidalgo.Agent.Prompts;
using Fidalgo.Agent.Tools;
using System.Text.Json;

namespace Fidalgo.Agent.Agents;

public class JobSearchAgent
{
    private readonly ChatClient _chatClient;
    private readonly FetchTool _fetchTool;
    private readonly SaveJobTool _saveJobTool;
    private readonly GetJobsTool _getJobsTool;
    private readonly string _email;
    private readonly string _resume;
    private readonly string? _narrative;

    public JobSearchAgent(
        ChatClient chatClient,
        FetchTool fetchTool,
        SaveJobTool saveJobTool,
        GetJobsTool getJobsTool,
        string email,
        string resume,
        string? narrative = null)
    {
        _chatClient = chatClient;
        _fetchTool = fetchTool;
        _saveJobTool = saveJobTool;
        _getJobsTool = getJobsTool;
        _email = email;
        _resume = resume;
        _narrative = narrative;
    }

    public async Task<int> RunAsync(
        string keywords,
        int maxPages = 100,
        CancellationToken cancellationToken = default)
    {
        var query = BuildSearchQuery(keywords);
        var prompt = AgentPrompt.Generate(_email, query, _narrative);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(prompt),
            new UserChatMessage($"Search for jobs: {keywords}. My resume: {_resume}")
        };

        if (!string.IsNullOrEmpty(_narrative))
        {
            messages.Add(new UserChatMessage($"Additional context: {_narrative}"));
        }

        var toolFetch = ChatTool.CreateFunctionTool(
            functionName: "fetch",
            functionDescription: "Fetch a web page and return sanitized content",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"url":{"type":"string","description":"The URL to fetch"}},"required":["url"]}"""));

        var toolSaveJob = ChatTool.CreateFunctionTool(
            functionName: "save_job",
            functionDescription: "Save a job with analysis results",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"employer":{"type":"string","description":"Employer name"},"employerJobId":{"type":"string","description":"Job ID from employer"},"postedDate":{"type":"string","description":"Posted date"},"salaryRangeLow":{"type":"number","description":"Low end of salary range"},"salaryRangeHigh":{"type":"number","description":"High end of salary range"},"description":{"type":"string","description":"Job description"},"pros":{"type":"string","description":"Pros of this job"},"cons":{"type":"string","description":"Cons of this job"},"resumeHints":{"type":"string","description":"How this job matches the resume"},"score":{"type":"integer","description":"Score from 0-100"},"recommendation":{"type":"string","description":"Recommendation"},"sourceWebsite":{"type":"string","description":"Source website"}},"required":["employer","description","pros","cons","resumeHints","score","recommendation","sourceWebsite"]}"""));

        var toolGetJobs = ChatTool.CreateFunctionTool(
            functionName: "get_jobs",
            functionDescription: "Query saved jobs by filters",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"keywords":{"type":"string","description":"Keywords to search"},"employer":{"type":"string","description":"Employer name"},"dateFrom":{"type":"string","description":"Date from"},"dateTo":{"type":"string","description":"Date to"},"sourceWebsite":{"type":"string","description":"Source website"}},"required":[]}}"""));

        var options = new ChatCompletionOptions
        {
            Tools = { toolFetch, toolSaveJob, toolGetJobs }
        };

        int messageCount = 0;
        bool requiresAction;

        do
        {
            requiresAction = false;
            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);

            messageCount++;

            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    messages.Add(new AssistantChatMessage(completion));
                    break;

                case ChatFinishReason.ToolCalls:
                    messages.Add(new AssistantChatMessage(completion));

                    foreach (ChatToolCall toolCall in completion.ToolCalls)
                    {
                        string toolResult = await ResolveToolCallAsync(toolCall, cancellationToken);
                        messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                    }

                    requiresAction = true;
                    break;

                default:
                    throw new NotImplementedException($"Chat finish reason {completion.FinishReason} is not implemented");
            }
        } while (requiresAction);

        return messageCount;
    }

    private async Task<string> ResolveToolCallAsync(ChatToolCall toolCall, CancellationToken cancellationToken)
    {
        if (toolCall.FunctionName == "fetch")
        {
            using JsonDocument arguments = JsonDocument.Parse(toolCall.FunctionArguments);
            string url = arguments.RootElement.GetProperty("url").GetString() ?? string.Empty;
            return await _fetchTool.FetchAsync(url, cancellationToken);
        }
        else if (toolCall.FunctionName == "save_job")
        {
            using JsonDocument arguments = JsonDocument.Parse(toolCall.FunctionArguments);
            Guid jobId = await SaveJobFromArgumentsAsync(arguments, cancellationToken);
            return $"Job saved with ID: {jobId}";
        }
        else if (toolCall.FunctionName == "get_jobs")
        {
            using JsonDocument arguments = JsonDocument.Parse(toolCall.FunctionArguments);
            var jobs = await _getJobsTool.GetAsync(_email, null, null, 
                GetOptionalString(arguments, "employer"),
                null, 
                GetOptionalString(arguments, "sourceWebsite"));
            return $"Found {jobs.Count} jobs";
        }
        else
        {
            throw new NotImplementedException($"Tool {toolCall.FunctionName} is not implemented");
        }
    }

    private async Task<Guid> SaveJobFromArgumentsAsync(JsonDocument arguments, CancellationToken cancellationToken)
    {
        string employer = arguments.RootElement.GetProperty("employer").GetString() ?? string.Empty;
        string? employerJobId = GetOptionalString(arguments, "employerJobId");
        DateTime? postedDate = GetOptionalDateTime(arguments, "postedDate");
        decimal? salaryRangeLow = GetOptionalDecimal(arguments, "salaryRangeLow");
        decimal? salaryRangeHigh = GetOptionalDecimal(arguments, "salaryRangeHigh");
        string description = arguments.RootElement.GetProperty("description").GetString() ?? string.Empty;
        string pros = arguments.RootElement.GetProperty("pros").GetString() ?? string.Empty;
        string cons = arguments.RootElement.GetProperty("cons").GetString() ?? string.Empty;
        string resumeHints = arguments.RootElement.GetProperty("resumeHints").GetString() ?? string.Empty;
        int score = arguments.RootElement.GetProperty("score").GetInt32();
        string recommendation = arguments.RootElement.GetProperty("recommendation").GetString() ?? string.Empty;
        string sourceWebsite = arguments.RootElement.GetProperty("sourceWebsite").GetString() ?? string.Empty;

        return await _saveJobTool.SaveAsync(
            _email,
            employer,
            employerJobId,
            postedDate,
            salaryRangeLow,
            salaryRangeHigh,
            description,
            pros,
            cons,
            resumeHints,
            score,
            recommendation,
            sourceWebsite,
            cancellationToken);
    }

    private static string? GetOptionalString(JsonDocument arguments, string propertyName)
    {
        return arguments.RootElement.TryGetProperty(propertyName, out JsonElement element) ? element.GetString() : null;
    }

    private static DateTime? GetOptionalDateTime(JsonDocument arguments, string propertyName)
    {
        return arguments.RootElement.TryGetProperty(propertyName, out JsonElement element) && !element.GetBoolean() ? DateTime.Parse(element.GetString() ?? string.Empty) : null;
    }

    private static decimal? GetOptionalDecimal(JsonDocument arguments, string propertyName)
    {
        return arguments.RootElement.TryGetProperty(propertyName, out JsonElement element) ? element.GetDecimal() : null;
    }

    private string BuildSearchQuery(string keywords)
    {
        return $"Search for jobs matching: {keywords}";
    }

    private async Task<Guid> SaveJobWrapper(
        string employer,
        string? employerJobId,
        DateTime? postedDate,
        decimal? salaryRangeLow,
        decimal? salaryRangeHigh,
        string description,
        string pros,
        string cons,
        string resumeHints,
        int score,
        string recommendation,
        string sourceWebsite,
        CancellationToken cancellationToken = default)
    {
        return await _saveJobTool.SaveAsync(
            _email,
            employer,
            employerJobId,
            postedDate,
            salaryRangeLow,
            salaryRangeHigh,
            description,
            pros,
            cons,
            resumeHints,
            score,
            recommendation,
            sourceWebsite,
            cancellationToken);
    }
}
