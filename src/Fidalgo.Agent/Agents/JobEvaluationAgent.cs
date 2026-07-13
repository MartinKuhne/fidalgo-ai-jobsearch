using OpenAI.Chat;
using Fidalgo.Shared.Storage;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Logging;
using Fidalgo.Shared.ErrorHandling;

using Fidalgo.Shared.Tools;
namespace Fidalgo.Agent.Agents;

/// <summary>
/// Core AI agent that evaluates a single job against the user's resume and updates the database.
/// </summary>
public class JobEvaluationAgent
{
    private readonly ChatClient _chatClient;
    private readonly JobRepository _repository;
    private readonly string _email;
    private readonly string _resume;
    private readonly ILogger<JobEvaluationAgent> _logger;

    public JobEvaluationAgent(
        ChatClient chatClient,
        JobRepository repository,
        string email,
        string resume,
        ILogger<JobEvaluationAgent> logger)
    {
        _chatClient = chatClient;
        _repository = repository;
        _email = email;
        _resume = resume;
        _logger = logger;
    }

    /// <summary>Evaluates a job and updates the entity.</summary>
    public async Task<int> EvaluateAsync(
        JobEntity job,
        CancellationToken cancellationToken = default)
    {
        var resume = TruncateResume(_resume);
        
        string prompt = $@"You are an AI job evaluation assistant. Your task is to evaluate the following job posting against the user's resume.
User Email: {_email}
Resume:
{resume}

Job Title: {job.Title}
Employer: {job.Employer}
Job Description:
{job.Description}

Instructions:
1. Extract the required skills, preferred skills, pay, and location details from the job description.
2. Compare those details against the user's resume.
3. Determine a match score (0-100).
4. Determine the Pros (why it's a good fit) and Cons (potential concerns).
5. Generate Resume Hints (tailoring recommendations).
6. Provide a Recommendation. Must be EXACTLY ""Apply"", ""Maybe"", or ""Do not apply"".
7. Use the update_job tool to save your evaluation. DO NOT output formatted text, only call the tool.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(prompt)
        };

        var toolUpdateJob = ChatTool.CreateFunctionTool(
            functionName: "update_job",
            functionDescription: "Update a job with evaluation results",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"pros":{"type":"string","description":"Pros of this job"},"cons":{"type":"string","description":"Cons of this job"},"resumeHints":{"type":"string","description":"How this job matches the resume"},"score":{"type":"integer","description":"Score from 0-100"},"recommendation":{"type":"string","description":"Recommendation (Apply, Maybe, Do not apply)"}},"required":["pros","cons","resumeHints","score","recommendation"]}"""));

        var options = new ChatCompletionOptions
        {
            Tools = { toolUpdateJob },
            Temperature = 0.2f
        };

        int messageCount = 0;
        bool requiresAction;

        do
        {
            requiresAction = false;
            messageCount++;
            _logger.LogInformation("Calling LLM (Message #{MessageCount}) for job {JobId}", messageCount, job.InternalId);

            ChatCompletion completion;
            try
            {
                completion = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            }
            catch (System.ClientModel.ClientResultException ex) when (ex.Status == 400)
            {
                _logger.LogError(ex, "LLM API returned 400 Bad Request. Job ID: {JobId}", job.InternalId);
                return messageCount;
            }

            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    _logger.LogInformation("LLM completed successfully for job {JobId}", job.InternalId);
                    messages.Add(new AssistantChatMessage(completion));
                    break;

                case ChatFinishReason.ToolCalls:
                    messages.Add(new AssistantChatMessage(completion));

                    foreach (ChatToolCall toolCall in completion.ToolCalls)
                    {
                        if (toolCall.FunctionName == "update_job")
                        {
                            using JsonDocument arguments = JsonDocument.Parse(toolCall.FunctionArguments);
                            
                            job.Pros = arguments.RootElement.GetProperty("pros").GetString() ?? string.Empty;
                            job.Cons = arguments.RootElement.GetProperty("cons").GetString() ?? string.Empty;
                            job.ResumeHints = arguments.RootElement.GetProperty("resumeHints").GetString() ?? string.Empty;
                            job.Score = arguments.RootElement.GetProperty("score").GetInt32();
                            job.Recommendation = arguments.RootElement.GetProperty("recommendation").GetString() ?? "Maybe";

                            if (job.Score < 0 || job.Score > 100) job.Score = 0;
                            if (job.Recommendation != "Apply" && job.Recommendation != "Maybe" && job.Recommendation != "Do not apply")
                            {
                                job.Recommendation = "Maybe";
                            }

                            await _repository.UpdateAsync(job, cancellationToken);
                            _logger.LogInformation("Updated job {JobId} with score {Score} and recommendation {Rec}", job.InternalId, job.Score, job.Recommendation);
                            
                            messages.Add(new ToolChatMessage(toolCall.Id, "Job successfully updated."));
                            // Only one tool call needed to save
                            return messageCount;
                        }
                    }

                    requiresAction = true;
                    break;

                case ChatFinishReason.Length:
                case ChatFinishReason.ContentFilter:
                case ChatFinishReason.FunctionCall:
                    throw new AgentExecutionException($"LLM aborted: {completion.FinishReason}");
            }
        } while (requiresAction);

        return messageCount;
    }

    private static string TruncateResume(string resume)
    {
        const int maxChars = 60000;
        if (resume.Length <= maxChars)
            return resume;

        return resume[..maxChars];
    }
}