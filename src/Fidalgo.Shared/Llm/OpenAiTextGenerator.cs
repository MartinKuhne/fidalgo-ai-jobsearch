using System.ClientModel;
using System.Diagnostics;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Polly;
using Polly.Retry;

namespace Fidalgo.Shared.Llm;

/// <summary>
/// <see cref="ITextGenerator"/> implementation backed by <see cref="ChatClient"/> with
/// Polly exponential-backoff retries, linked timeout enforcement, response validation,
/// and OpenTelemetry <see cref="Activity"/> tracing.
/// </summary>
public class OpenAiTextGenerator : ITextGenerator
{
    private static readonly ActivitySource ActivitySource = new("Fidalgo.ApplicationPacket");
    private static readonly TimeSpan DefaultRetryBaseDelay = TimeSpan.FromSeconds(1);

    private readonly ChatClient _chatClient;
    private readonly IOptions<ApplicationPacketOptions> _packetOptions;
    private readonly ILogger<OpenAiTextGenerator> _logger;
    private readonly TimeSpan _retryBaseDelay;

    /// <summary>Initializes a new instance of <see cref="OpenAiTextGenerator"/>.</summary>
    public OpenAiTextGenerator(
        ChatClient chatClient,
        IOptions<ApplicationPacketOptions> packetOptions,
        ILogger<OpenAiTextGenerator> logger)
        : this(chatClient, packetOptions, logger, DefaultRetryBaseDelay)
    {
    }

    internal OpenAiTextGenerator(
        ChatClient chatClient,
        IOptions<ApplicationPacketOptions> packetOptions,
        ILogger<OpenAiTextGenerator> logger,
        TimeSpan retryBaseDelay)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(packetOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _chatClient = chatClient;
        _packetOptions = packetOptions;
        _logger = logger;
        _retryBaseDelay = retryBaseDelay;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        TextGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);

        var effectiveOptions = options ?? new TextGenerationOptions();
        var timeoutSeconds = Math.Max(1, _packetOptions.Value.LlmTimeoutSeconds);
        var timeout = effectiveOptions.Timeout ?? TimeSpan.FromSeconds(timeoutSeconds);

        using var activity = ActivitySource.StartActivity("llm.generate", ActivityKind.Client);
        activity?.SetTag("llm.temperature", effectiveOptions.Temperature);
        activity?.SetTag("llm.prompt_chars", systemPrompt.Length + userPrompt.Length);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var chatOptions = new ChatCompletionOptions
        {
            Temperature = effectiveOptions.Temperature
        };

        if (effectiveOptions.MaxOutputTokens.HasValue)
        {
            chatOptions.MaxOutputTokenCount = effectiveOptions.MaxOutputTokens.Value;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var pipeline = BuildRetryPipeline(cancellationToken, timeoutCts.Token);

        ChatCompletion completion;
        try
        {
            completion = await pipeline.ExecuteAsync(async ct =>
            {
                var result = await _chatClient.CompleteChatAsync(messages, chatOptions, ct);
                return result.Value;
            }, timeoutCts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            _logger.LogError(ex, "LLM generation timed out after {TimeoutSeconds}s.", timeout.TotalSeconds);
            throw new TimeoutException($"LLM generation timed out after {timeout.TotalSeconds:0.#}s.", ex);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AgentExecutionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM generation failed with unexpected error.");
            throw new AgentExecutionException($"LLM generation failed: {ex.Message}", ex);
        }

        ValidateCompletion(completion);

        var text = string.Concat(completion.Content.Select(part => part.Text ?? string.Empty));
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new AgentExecutionException("LLM returned empty content.");
        }

        activity?.SetTag("llm.output_chars", text.Length);
        return text;
    }

    private ResiliencePipeline BuildRetryPipeline(CancellationToken callerToken, CancellationToken linkedTimeoutToken)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = _retryBaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<ClientResultException>(IsTransientClientError)
                    .Handle<TaskCanceledException>(_ => !callerToken.IsCancellationRequested && !linkedTimeoutToken.IsCancellationRequested),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Transient LLM error on attempt {AttemptNumber}; retrying in {RetryDelayMs}ms.",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds);
                    return default;
                }
            })
            .Build();
    }

    private static bool IsTransientClientError(ClientResultException ex) =>
        ex.Status is 408 or 429 or (>= 500 and < 600);

    private static void ValidateCompletion(ChatCompletion? completion)
    {
        if (completion is null)
        {
            throw new AgentExecutionException("LLM returned a null completion.");
        }

        if (!string.IsNullOrWhiteSpace(completion.Refusal))
        {
            throw new AgentExecutionException($"LLM refused the request: {completion.Refusal}");
        }

        if (completion.FinishReason is ChatFinishReason.ContentFilter or ChatFinishReason.Length)
        {
            throw new AgentExecutionException($"LLM aborted generation with finish reason: {completion.FinishReason}");
        }

        if (completion.Content is null || completion.Content.Count == 0)
        {
            throw new AgentExecutionException("LLM returned no content parts.");
        }
    }
}
