using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Llm;
using Fidalgo.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenAI.Chat;

namespace Fidalgo.Shared.Tests.Llm;

public class OpenAiTextGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_ReturnsCompletionText_AndEmitsActivity()
    {
        var chatClient = Substitute.For<ChatClient>();
        chatClient
            .CompleteChatAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatCompletionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(CreateCompletionResult("# Tailored Resume"));

        var observedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Fidalgo.ApplicationPacket",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => observedActivities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        var sut = CreateSut(chatClient);
        var result = await sut.GenerateAsync("system", "user", new TextGenerationOptions(Temperature: 0.1f, MaxOutputTokens: 500));

        Assert.Equal("# Tailored Resume", result);
        Assert.Contains(observedActivities, a => a.OperationName == "llm.generate");
    }

    [Fact]
    public async Task GenerateAsync_RetriesOnTransientClientResultException_ThenSucceeds()
    {
        var chatClient = Substitute.For<ChatClient>();
        var callCount = 0;

        chatClient
            .CompleteChatAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatCompletionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns<ClientResult<ChatCompletion>>(_ =>
            {
                callCount++;
                if (callCount < 3)
                {
                    throw CreateClientResultException(503);
                }

                return CreateCompletionResult("Recovered response");
            });

        var sut = CreateSut(chatClient);
        var result = await sut.GenerateAsync("system", "user");

        Assert.Equal("Recovered response", result);
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task GenerateAsync_DoesNotRetryNonTransient400Error_AndThrowsAgentExecutionException()
    {
        var chatClient = Substitute.For<ChatClient>();
        var callCount = 0;

        chatClient
            .CompleteChatAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatCompletionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns<ClientResult<ChatCompletion>>(_ =>
            {
                callCount++;
                throw CreateClientResultException(400);
            });

        var sut = CreateSut(chatClient);

        await Assert.ThrowsAsync<AgentExecutionException>(() => sut.GenerateAsync("system", "user"));
        Assert.Equal(1, callCount);
    }

    [Theory]
    [InlineData("stop", "   ", null)]
    [InlineData("length", "Partial output", null)]
    [InlineData("content_filter", "Blocked", null)]
    [InlineData("stop", "", "Cannot fulfill")]
    public async Task GenerateAsync_ThrowsAgentExecutionException_OnEmptyRefusalOrAbnormalFinish(
        string finishReason,
        string content,
        string? refusal)
    {
        var chatClient = Substitute.For<ChatClient>();
        chatClient
            .CompleteChatAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatCompletionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(CreateCompletionResult(content, finishReason, refusal));

        var sut = CreateSut(chatClient);

        await Assert.ThrowsAsync<AgentExecutionException>(() => sut.GenerateAsync("system", "user"));
    }

    [Fact]
    public async Task GenerateAsync_ThrowsTimeoutException_WhenPerCallTimeoutExpires()
    {
        var chatClient = Substitute.For<ChatClient>();
        chatClient
            .CompleteChatAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatCompletionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(2);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return CreateCompletionResult("Too late");
            });

        var sut = CreateSut(chatClient);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            sut.GenerateAsync("system", "user", new TextGenerationOptions(Timeout: TimeSpan.FromMilliseconds(30))));
    }

    [Fact]
    public async Task GenerateAsync_PropagatesOperationCanceledException_WhenCallerCancels()
    {
        var chatClient = Substitute.For<ChatClient>();
        using var callerCts = new CancellationTokenSource();

        chatClient
            .CompleteChatAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatCompletionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(2);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return CreateCompletionResult("Never");
            });

        var sut = CreateSut(chatClient);
        callerCts.CancelAfter(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.GenerateAsync("system", "user", new TextGenerationOptions(Timeout: TimeSpan.FromSeconds(10)), callerCts.Token));
    }

    [Fact]
    public void AddFidalgoLlmClient_RegistersChatClientAndTextGenerator()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LLM:Endpoint"] = "http://localhost:8080/v1",
                ["LLM:Model"] = "test-model",
                ["LLM:ApiKey"] = "test-key"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFidalgoLlmClient(config);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<ChatClient>());
        Assert.IsType<OpenAiTextGenerator>(provider.GetRequiredService<ITextGenerator>());
    }

    private static OpenAiTextGenerator CreateSut(ChatClient chatClient) =>
        new(
            chatClient,
            Options.Create(new ApplicationPacketOptions { LlmTimeoutSeconds = 30 }),
            NullLogger<OpenAiTextGenerator>.Instance,
            TimeSpan.FromMilliseconds(5));

    private static ClientResult<ChatCompletion> CreateCompletionResult(
        string content,
        string finishReason = "stop",
        string? refusal = null)
    {
        var refusalJson = refusal is null ? "null" : $"\"{refusal}\"";
        var escapedContent = content.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        var json = $$"""
            {
              "id": "chatcmpl-test",
              "object": "chat.completion",
              "created": 1700000000,
              "model": "test-model",
              "choices": [
                {
                  "index": 0,
                  "finish_reason": "{{finishReason}}",
                  "message": {
                    "role": "assistant",
                    "content": "{{escapedContent}}",
                    "refusal": {{refusalJson}}
                  }
                }
              ]
            }
            """;

        var completion = ModelReaderWriter.Read<ChatCompletion>(BinaryData.FromString(json))!;
        var pipelineResponse = Substitute.For<PipelineResponse>();
        return ClientResult.FromValue(completion, pipelineResponse);
    }

    private static ClientResultException CreateClientResultException(int statusCode)
    {
        var response = Substitute.For<PipelineResponse>();
        response.Status.Returns(statusCode);
        return new ClientResultException($"HTTP {statusCode}", response);
    }
}
