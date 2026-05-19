using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Fidalgo.Agent.Prompts;
using Fidalgo.Agent.Tools;

namespace Fidalgo.Agent.Agents;

public class JobSearchAgent
{
    private readonly IChatClient _chatClient;
    private readonly FetchTool _fetchTool;
    private readonly SaveJobTool _saveJobTool;
    private readonly GetJobsTool _getJobsTool;
    private readonly string _email;
    private readonly string _resume;
    private readonly string? _narrative;

    public JobSearchAgent(
        IChatClient chatClient,
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

        var fetchFunc = AIFunctionFactory.Create(_fetchTool.FetchAsync, "fetch", "Fetch a web page and return sanitized content");
        var saveJobFunc = AIFunctionFactory.Create(SaveJobWrapper, "save_job", "Save a job with analysis results");
        var getJobsFunc = AIFunctionFactory.Create(_getJobsTool.GetAsync, "get_jobs", "Query saved jobs by filters");

        var agent = _chatClient.AsAIAgent(
            instructions: prompt,
            name: "JobSearchAgent",
            tools: [fetchFunc, saveJobFunc, getJobsFunc]);

        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync(session);
        
        return response.Messages.Count;
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
