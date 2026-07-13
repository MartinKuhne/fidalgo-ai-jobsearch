using OpenAI.Chat;
using Fidalgo.Ingest.Prompts;
using Fidalgo.Ingest.Tools;
using Fidalgo.Shared.Tools;
using Fidalgo.Shared.Models;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Logging;
using Fidalgo.Shared.ErrorHandling;


namespace Fidalgo.Ingest.Agents;

/// <summary>
/// Core AI agent that orchestrates an LLM chat loop with tool calling for job searching.
/// Builds search queries, truncates resume, resolves tool calls to browser fetch/save/query operations,
/// and manages message history between the LLM and agent tools.
/// </summary>
public class JobSearchAgent
{
    private readonly ChatClient _chatClient;
    private readonly IBrowserFetchTool _browserFetchTool;
    private readonly SaveJobTool _saveJobTool;
    private readonly GetJobsTool _getJobsTool;
    private readonly JobInDbTool _jobInDbTool;
    private readonly WebSearchTool _webSearchTool;
    private readonly MarkdownFetchTool _markdownFetchTool;
    private readonly DelegateTool _delegateTool;
    private readonly string _email;
    private readonly string _zipCode;
    private readonly ILogger<JobSearchAgent> _logger;

    /// <summary>Initializes a new instance of the JobSearchAgent.</summary>
    /// <param name="chatClient">Chat client for LLM communication.</param>
    /// <param name="browserFetchTool">Tool for fetching web pages via Playwright.</param>
    /// <param name="saveJobTool">Tool for saving jobs.</param>
    /// <param name="getJobsTool">Tool for querying saved jobs.</param>
    /// <param name="jobInDbTool">Tool for checking job existence in database.</param>
    /// <param name="webSearchTool">Tool for web search via SearxNG.</param>
    /// <param name="markdownFetchTool">Tool for fetching pages as markdown via url-to-markdown.</param>
    /// <param name="delegateTool">Tool for delegating work to a sub-agent.</param>
    /// <param name="email">The tenant's email address.</param>
    /// <param name="zipCode">The target zip code.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public JobSearchAgent(
        ChatClient chatClient,
        IBrowserFetchTool browserFetchTool,
        SaveJobTool saveJobTool,
        GetJobsTool getJobsTool,
        JobInDbTool jobInDbTool,
        WebSearchTool webSearchTool,
        MarkdownFetchTool markdownFetchTool,
        DelegateTool delegateTool,
        string email,
        string zipCode,
        ILogger<JobSearchAgent> logger = null!)
    {
        _chatClient = chatClient;
        _browserFetchTool = browserFetchTool;
        _saveJobTool = saveJobTool;
        _getJobsTool = getJobsTool;
        _jobInDbTool = jobInDbTool;
        _webSearchTool = webSearchTool;
        _markdownFetchTool = markdownFetchTool;
        _delegateTool = delegateTool;
        _email = email;
        _zipCode = zipCode;
        _logger = logger;
    }

    /// <summary>Runs the agent's search loop with the LLM and tools.</summary>
    /// <param name="keywords">Search keywords for the agent to use.</param>
    /// <param name="maxPages">Maximum number of pages to search (default: 100).</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>Number of LLM messages exchanged.</returns>
    public async Task<int> RunAsync(
        string keywords,
        int maxPages = 100,
        CancellationToken cancellationToken = default)
    {
        var query = BuildSearchQuery(keywords);
        var prompt = AgentPrompt.Generate(_email, query, _zipCode);

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
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"employer":{"type":"string","description":"Employer name"},"title":{"type":"string","description":"Job title"},"employerJobId":{"type":"string","description":"Job ID from employer"},"postedDate":{"type":"string","description":"Posted date"},"salaryRangeLow":{"type":"number","description":"Low end of salary range"},"salaryRangeHigh":{"type":"number","description":"High end of salary range"},"jobUrl":{"type":"string","description":"URL to the job posting"},"sourceWebsite":{"type":"string","description":"Source website"}},"required":["employer","jobUrl","sourceWebsite"]}"""));

        var toolGetJobs = ChatTool.CreateFunctionTool(
            functionName: "get_jobs",
            functionDescription: "Query saved jobs by filters",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"keywords":{"type":"string","description":"Keywords to search"},"employer":{"type":"string","description":"Employer name"},"dateFrom":{"type":"string","description":"Date from"},"dateTo":{"type":"string","description":"Date to"},"sourceWebsite":{"type":"string","description":"Source website"}},"required":[]}"""));

        var toolJobInDb = ChatTool.CreateFunctionTool(
            functionName: "job_in_db",
            functionDescription: "Check if a job already exists in the local database",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"email":{"type":"string","description":"User email address"},"siteUrl":{"type":"string","description":"Job board site URL (e.g., indeed.com)"},"jobId":{"type":"string","description":"Job ID from the employer"}},"required":["email","siteUrl","jobId"]}"""));

        var toolWebSearch = ChatTool.CreateFunctionTool(
            functionName: "web_search",
            functionDescription: "Search the web using SearxNG meta-search engine",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"query":{"type":"string","description":"Search query"}},"required":["query"]}"""));

        var toolMarkdownFetch = ChatTool.CreateFunctionTool(
            functionName: "markdown_fetch",
            functionDescription: "Fetch a web page and convert it to clean markdown using the url-to-markdown service",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"url":{"type":"string","description":"The URL to fetch"}},"required":["url"]}"""));

        var toolDelegate = ChatTool.CreateFunctionTool(
            functionName: "delegate",
            functionDescription: "Delegate a task to a sub-agent for isolated processing. Use this when you have large content that would pollute the conversation context or for parallelizable work.",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"task":{"type":"string","description":"Instructions for the sub-agent"},"content":{"type":"string","description":"Content for the sub-agent to process"}},"required":["task","content"]}"""));

        var options = new ChatCompletionOptions
        {
            Tools = { toolFetch, toolSaveJob, toolGetJobs, toolJobInDb, toolWebSearch, toolMarkdownFetch, toolDelegate }
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
                    throw new AgentExecutionException("Incomplete model output due to MaxTokens parameter or token limit exceeded.");

                case ChatFinishReason.ContentFilter:
                    throw new AgentExecutionException("Omitted content due to a content filter flag.");

                case ChatFinishReason.FunctionCall:
                    throw new AgentExecutionException("Deprecated in favor of tool calls.");

                default:
                    throw new AgentExecutionException($"Unexpected chat finish reason: {completion.FinishReason}");
            }
        } while (requiresAction);

        var lastAssistantMessage = messages.OfType<AssistantChatMessage>().LastOrDefault();
        if (lastAssistantMessage?.Content is not null)
        {
            _logger.LogInformation("\n--- Final Response ---\n{Response}\n----------------------", lastAssistantMessage.Content.ToString());
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
            if (result.PageType == PageType.CloudflareChallenge)
            {
                _logger.LogWarning("Cloudflare challenge detected for URL: {Url}", url);
                return $"Fetch failed: Cloudflare challenge detected for {url}. The website is blocking automated access with a security verification that could not be completed.";
            }
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
        else if (toolCall.FunctionName == "web_search")
        {
            using JsonDocument arguments = JsonDocument.Parse(toolCall.FunctionArguments);
            string query = arguments.RootElement.GetProperty("query").GetString() ?? string.Empty;
            _logger.LogInformation("Tool web_search: query={Query}", query);
            return await _webSearchTool.SearchAsync(query, cancellationToken);
        }
        else if (toolCall.FunctionName == "markdown_fetch")
        {
            using JsonDocument arguments = JsonDocument.Parse(toolCall.FunctionArguments);
            string url = arguments.RootElement.GetProperty("url").GetString() ?? string.Empty;
            _logger.LogInformation("Tool markdown_fetch: URL={Url}", url);
            return await _markdownFetchTool.FetchAsync(url, cancellationToken);
        }
        else if (toolCall.FunctionName == "delegate")
        {
            using JsonDocument arguments = JsonDocument.Parse(toolCall.FunctionArguments);
            string task = arguments.RootElement.GetProperty("task").GetString() ?? string.Empty;
            string content = arguments.RootElement.GetProperty("content").GetString() ?? string.Empty;
            _logger.LogInformation("Tool delegate: task={Task}", task);
            return await _delegateTool.DelegateAsync(task, content, cancellationToken);
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
        string jobUrl = arguments.RootElement.GetProperty("jobUrl").GetString() ?? string.Empty;

        string sourceWebsite = arguments.RootElement.GetProperty("sourceWebsite").GetString() ?? string.Empty;

        return await _saveJobTool.SaveAsync(
            _email,
            employer,
            title,
            employerJobId,
            postedDate,
            salaryRangeLow,
            salaryRangeHigh,
            jobUrl,
            sourceWebsite,
            cancellationToken: cancellationToken);
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

    private static string BuildSearchQuery(string keywords)
    {
        return $"Search for jobs matching: {keywords}";
    }
}