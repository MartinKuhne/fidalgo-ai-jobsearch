using OpenAI.Chat;
using Fidalgo.Shared.Pipeline;
using Fidalgo.Shared.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly string? _careerStory;
    private readonly string? _careerGoals;
    private readonly ILogger<LlmJobEvaluationService> _serviceLogger;

    public JobEvaluationAgent(
        ChatClient chatClient,
        JobRepository repository,
        string email,
        string resume,
        ILogger<JobEvaluationAgent> logger,
        string? careerStory = null,
        string? careerGoals = null)
    {
        _chatClient = chatClient;
        _repository = repository;
        _email = email;
        _resume = resume;
        _careerStory = careerStory;
        _careerGoals = careerGoals;
        _serviceLogger = logger is null
            ? NullLogger<LlmJobEvaluationService>.Instance
            : new LoggerAdapter(logger);
    }

    /// <summary>Evaluates a job and updates the entity.</summary>
    public Task<int> EvaluateAsync(
        JobEntity job,
        CancellationToken cancellationToken = default)
    {
        var service = new LlmJobEvaluationService(_chatClient, _repository, _serviceLogger);
        return service.EvaluateJobAsync(
            job,
            _email,
            _resume,
            _careerStory,
            _careerGoals,
            cancellationToken);
    }

    /// <summary>Builds the LLM system prompt for evaluating a job against the user's resume and optional career story and career goals.</summary>
    public string BuildPrompt(JobEntity job) =>
        BuildPrompt(_email, _resume, _careerStory, _careerGoals, job);

    /// <summary>Pure-function variant of the prompt builder. Exposed publicly so tests can exercise the template without instantiating the agent.</summary>
    public static string BuildPrompt(
        string email,
        string resume,
        string? careerStory,
        string? careerGoals,
        JobEntity job) =>
        JobEvaluationPromptBuilder.BuildPrompt(email, resume, careerStory, careerGoals, job);

    private sealed class LoggerAdapter(ILogger inner) : ILogger<LlmJobEvaluationService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            inner.Log(logLevel, eventId, state, exception, formatter);
    }
}