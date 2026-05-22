using OpenAI.Chat;
    using Fidalgo.Agent.Prompts;
    using Fidalgo.Agent.Tools;
    using Fidalgo.Agent.Models;
using System.Text.Json;
using System.Linq;
    using Microsoft.Extensions.Logging;

namespace Fidalgo.Agent.Agents;

public class JobSearchAgent
{
    private readonly ChatClient _chatClient;
    private readonly IBrowserFetchTool _browserFetchTool;
    private readonly SaveJobTool _saveJobTool;
    private readonly GetJobsTool _getJobsTool;
    private readonly JobInDbTool _jobInDbTool;
    private readonly string _email;
    private readonly string _resume;
    private readonly string _zipCode;
    private readonly ILogger<JobSearchAgent> _logger;

    public JobSearchAgent(
        ChatClient chatClient,
        IBrowserFetchTool browserFetchTool,
        SaveJobTool saveJobTool,
        GetJobsTool getJobsTool,
        JobInDbTool jobInDbTool,
        string email,
        string resume,
        string zipCode,
        ILogger<JobSearchAgent> logger = null!)
    {
        _chatClient = chatClient;
        _browserFetchTool = browserFetchTool;
        _saveJobTool = saveJobTool;
        _getJobsTool = getJobsTool;
        _jobInDbTool = jobInDbTool;
        _email = email;
        _resume = resume;
        _zipCode = zipCode;
        _logger = logger;
    }

    public async Task<int> RunAsync(
        string keywords,
        int maxPages = 100,
        CancellationToken cancellationToken = default)
    {
        var query = BuildSearchQuery(keywords);
        var resume = TruncateResume(_resume);
        var prompt = AgentPrompt.Generate(_email, query, resume, _zipCode);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(prompt)
        };

        var toolFetch = ChatTool.CreateFunctionTool(
            functionName: "playwright_fetch",
            functionDescription: "Fetch a web page and return sanitized content",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"url":{"type":"string","description":"The URL to fetch"}},"required":["url"]}"""));

        var toolSaveJob = ChatTool.CreateFunctionTool(
            functionName: "save_job",
            functionDescription: "Save a job with analysis results",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"employer":{"type":"string","description":"Employer name"},"title":{"type":"string","description":"Job title"},"employerJobId":{"type":"string","description":"Job ID from employer"},"postedDate":{"type":"string","description":"Posted date"},"salaryRangeLow":{"type":"number","description":"Low end of salary range"},"salaryRangeHigh":{"type":"number","description":"High end of salary range"},"description":{"type":"string","description":"Job description"},"pros":{"type":"string","description":"Pros of this job"},"cons":{"type":"string","description":"Cons of this job"},"resumeHints":{"type":"string","description":"How this job matches the resume"},"score":{"type":"integer","description":"Score from 0-100"},"recommendation":{"type":"string","description":"Recommendation"},"sourceWebsite":{"type":"string","description":"Source website"}},"required":["employer","description","pros","cons","resumeHints","score","recommendation","sourceWebsite"]}"""));

        var toolGetJobs = ChatTool.CreateFunctionTool(
            functionName: "get_jobs",
            functionDescription: "Query saved jobs by filters",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"keywords":{"type":"string","description":"Keywords to search"},"employer":{"type":"string","description":"Employer name"},"dateFrom":{"type":"string","description":"Date from"},"dateTo":{"type":"string","description":"Date to"},"sourceWebsite":{"type":"string","description":"Source website"}},"required":[]}"""));

        var toolJobInDb = ChatTool.CreateFunctionTool(
            functionName: "job_in_db",
            functionDescription: "Check if a job already exists in the local database",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"email":{"type":"string","description":"User email address"},"siteUrl":{"type":"string","description":"Job board site URL (e.g., indeed.com)"},"jobId":{"type":"string","description":"Job ID from the employer"}},"required":["email","siteUrl","jobId"]}"""));

        var options = new ChatCompletionOptions
        {
            Tools = { toolFetch, toolSaveJob, toolGetJobs, toolJobInDb }
        };

        int messageCount = 0;
        bool requiresAction;

        do
        {
            requiresAction = false;
            messageCount++;
            _logger.LogInformation("Calling LLM (Message #{MessageCount})", messageCount);

            ChatCompletion completion;
            try
            {
                completion = _chatClient.CompleteChat(messages, options, cancellationToken);
            }
            catch (System.ClientModel.ClientResultException ex) when (ex.Status == 400)
            {
                _logger.LogError(ex, "LLM API returned 400 Bad Request. This may be due to the request being too large.");
                _logger.LogError("Messages count: {Count}, Total content length: {TotalLength}", messages.Count, string.Join("", messages.Select(m => m.Content.ToString())).Length);
                throw;
            }

            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    _logger.LogInformation("LLM completed successfully");
                    messages.Add(new AssistantChatMessage(completion));
                    break;

                case ChatFinishReason.ToolCalls:
                    messages.Add(new AssistantChatMessage(completion));

                    foreach (ChatToolCall toolCall in completion.ToolCalls)
                    {
                        _logger.LogInformation("Executing tool call: {ToolName} with arguments: {Arguments}",
                            toolCall.FunctionName, toolCall.FunctionArguments);
                        string toolResult = await ResolveToolCallAsync(toolCall, cancellationToken);
                        _logger.LogInformation("Tool call {ToolName} returned result", toolCall.FunctionName);
                        messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                    }

                    requiresAction = true;
                    break;

                case ChatFinishReason.Length:
                    throw new NotImplementedException("Incomplete model output due to MaxTokens parameter or token limit exceeded.");

                case ChatFinishReason.ContentFilter:
                    throw new NotImplementedException("Omitted content due to a content filter flag.");

                case ChatFinishReason.FunctionCall:
                    throw new NotImplementedException("Deprecated in favor of tool calls.");

                default:
                    throw new NotImplementedException(completion.FinishReason.ToString());
            }
        } while (requiresAction);

        var lastAssistantMessage = messages.OfType<AssistantChatMessage>().LastOrDefault();
        if (lastAssistantMessage?.Content is not null)
        {
            Console.WriteLine("\n--- Final Response ---");
            Console.WriteLine(lastAssistantMessage.Content.ToString());
            Console.WriteLine("----------------------");
        }

        return messageCount;
    }

    private async Task<string> ResolveToolCallAsync(ChatToolCall toolCall, CancellationToken cancellationToken)
    {
        if (toolCall.FunctionName == "playwright_fetch")
        {
            using JsonDocument arguments = JsonDocument.Parse(toolCall.FunctionArguments);
            string url = arguments.RootElement.GetProperty("url").GetString() ?? string.Empty;
            _logger.LogInformation("Tool playwright_fetch: URL={Url}", url);
            var request = new FetchRequest(Url: url);
            var result = await _browserFetchTool.FetchAsync(request, cancellationToken);
            if (!string.IsNullOrEmpty(result.Error))
            {
                _logger.LogError("Tool playwright_fetch failed: {Error}", result.Error);
            }
            return result.Error ?? result.Content;
        }
        else if (toolCall.FunctionName == "save_job")
        {
            using JsonDocument arguments = JsonDocument.Parse(toolCall.FunctionArguments);
            _logger.LogInformation("Tool save_job: parsing arguments");
            Guid jobId = await SaveJobFromArgumentsAsync(arguments, cancellationToken);
            _logger.LogInformation("Tool save_job: saved job with ID {JobId}", jobId);
            return $"Job saved with ID: {jobId}";
        }
        else if (toolCall.FunctionName == "get_jobs")
        {
            using JsonDocument arguments = JsonDocument.Parse(toolCall.FunctionArguments);
            _logger.LogInformation("Tool get_jobs: querying jobs");
            var jobs = await _getJobsTool.GetAsync(_email, null, null, 
                GetOptionalString(arguments, "employer"),
                null, 
                GetOptionalString(arguments, "sourceWebsite"));
            _logger.LogInformation("Tool get_jobs: found {Count} jobs", jobs.Count);
            return $"Found {jobs.Count} jobs";
        }
        else if (toolCall.FunctionName == "job_in_db")
        {
            using JsonDocument arguments = JsonDocument.Parse(toolCall.FunctionArguments);
            string email = arguments.RootElement.GetProperty("email").GetString() ?? string.Empty;
            string siteUrl = arguments.RootElement.GetProperty("siteUrl").GetString() ?? string.Empty;
            string jobId = arguments.RootElement.GetProperty("jobId").GetString() ?? string.Empty;
            _logger.LogInformation("Tool job_in_db: checking email={Email}, siteUrl={SiteUrl}, jobId={JobId}", email, siteUrl, jobId);
            var exists = await _jobInDbTool.ContainsAsync(email, siteUrl, jobId, cancellationToken);
            _logger.LogInformation("Tool job_in_db: job exists={Exists}", exists);
            return exists.ToString().ToLowerInvariant();
        }
        else
        {
            throw new NotImplementedException($"Tool {toolCall.FunctionName} is not implemented");
        }
    }

    private async Task<Guid> SaveJobFromArgumentsAsync(JsonDocument arguments, CancellationToken cancellationToken)
    {
        string employer = arguments.RootElement.GetProperty("employer").GetString() ?? string.Empty;
        string? title = GetOptionalString(arguments, "title");
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
            title,
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
        if (!arguments.RootElement.TryGetProperty(propertyName, out JsonElement element))
            return null;
        return element.ValueKind == JsonValueKind.String ? DateTime.Parse(element.GetString() ?? string.Empty) : null;
    }

    private static decimal? GetOptionalDecimal(JsonDocument arguments, string propertyName)
    {
        return arguments.RootElement.TryGetProperty(propertyName, out JsonElement element) ? element.GetDecimal() : null;
    }

    private string BuildSearchQuery(string keywords)
    {
        return $"Search for jobs matching: {keywords}";
    }

    private string TruncateResume(string resume)
    {
        const int maxChars = 60000;
        if (resume.Length <= maxChars)
            return resume;

        _logger.LogWarning("Resume truncated from {OriginalLength} to {MaxChars} characters", resume.Length, maxChars);
        return resume[..maxChars];
    }

    private async Task<Guid> SaveJobWrapper(
        string employer,
        string? title,
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
            title,
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
