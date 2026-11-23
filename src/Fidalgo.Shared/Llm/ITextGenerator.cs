using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.Llm;

/// <summary>
/// Minimal abstraction for single-turn LLM text generation, enabling hermetic unit testing.
/// </summary>
public interface ITextGenerator
{
    /// <summary>
    /// Generates text from a system prompt and user prompt.
    /// </summary>
    /// <param name="systemPrompt">System instruction prompt.</param>
    /// <param name="userPrompt">User input prompt.</param>
    /// <param name="options">Optional generation settings (temperature, token limit, timeout).</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>The generated response text.</returns>
    Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        TextGenerationOptions? options = null,
        CancellationToken cancellationToken = default);
}
