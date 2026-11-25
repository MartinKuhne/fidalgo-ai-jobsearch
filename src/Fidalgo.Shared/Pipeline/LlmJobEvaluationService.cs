using System.ClientModel;
using System.Text.Json;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Storage;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Fidalgo.Shared.Pipeline;

/// <summary>
/// Evaluates untriaged jobs using <see cref="ChatClient"/> tool calling (<c>update_job</c>)
/// and persists evaluation scores and recommendations via <see cref="JobRepository"/>.
/// </summary>
public class LlmJobEvaluationService : IJobEvaluationService
{
    private readonly ChatClient _chatClient;
    private readonly JobRepository _repository;
    private readonly ILogger<LlmJobEvaluationService> _logger;

    /// <summary>Initializes a new instance of <see cref="LlmJobEvaluationService"/>.</summary>
    public LlmJobEvaluationService(
        ChatClient chatClient,
        JobRepository repository,
        ILogger<LlmJobEvaluationService> logger)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);

        _chatClient = chatClient;
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> EvaluateJobAsync(
        JobEntity job,
        string email,
        string resumeMarkdown,
        string? careerStoryMarkdown,
        string? careerGoalsMarkdown,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var prompt = JobEvaluationPromptBuilder.BuildPrompt(
            email,
            resumeMarkdown,
            careerStoryMarkdown,
            careerGoalsMarkdown,
            job);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(prompt)
        };

        var toolUpdateJob = ChatTool.CreateFunctionTool(
            functionName: "update_job",
            functionDescription: "Update a job with evaluation results",
            functionParameters: BinaryData.FromString(
                """{"type":"object","properties":{"pros":{"type":"string","description":"Pros of this job"},"cons":{"type":"string","description":"Cons of this job"},"resumeHints":{"type":"string","description":"How this job matches the resume"},"score":{"type":"integer","description":"Score from 0-100"},"recommendation":{"type":"string","description":"Recommendation (Apply, Maybe, Do not apply)"}},"required":["pros","cons","resumeHints","score","recommendation"]}"""));

        var options = new ChatCompletionOptions
        {
            Tools = { toolUpdateJob },
            Temperature = 0.2f
        };

        var messageCount = 0;
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
            catch (ClientResultException ex) when (ex.Status == 400)
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

                    foreach (var toolCall in completion.ToolCalls)
                    {
                        if (toolCall.FunctionName == "update_job")
                        {
                            using var arguments = JsonDocument.Parse(toolCall.FunctionArguments);

                            job.Pros = arguments.RootElement.GetProperty("pros").GetString() ?? string.Empty;
                            job.Cons = arguments.RootElement.GetProperty("cons").GetString() ?? string.Empty;
                            job.ResumeHints = arguments.RootElement.GetProperty("resumeHints").GetString() ?? string.Empty;
                            job.Score = arguments.RootElement.GetProperty("score").GetInt32();
                            job.Recommendation = arguments.RootElement.GetProperty("recommendation").GetString() ?? "Maybe";

                            if (job.Score is < 0 or > 100)
                            {
                                job.Score = 0;
                            }

                            if (job.Recommendation is not ("Apply" or "Maybe" or "Do not apply"))
                            {
                                job.Recommendation = "Maybe";
                            }

                            await _repository.UpdateAsync(job, cancellationToken);
                            _logger.LogInformation(
                                "Updated job {JobId} with score {Score} and recommendation {Rec}",
                                job.InternalId,
                                job.Score,
                                job.Recommendation);

                            messages.Add(new ToolChatMessage(toolCall.Id, "Job successfully updated."));
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
        }
        while (requiresAction);

        return messageCount;
    }
}
