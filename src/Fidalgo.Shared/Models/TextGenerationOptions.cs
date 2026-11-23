namespace Fidalgo.Shared.Models;

/// <summary>
/// Per-call options controlling LLM text generation behavior.
/// </summary>
public sealed record TextGenerationOptions(
    float Temperature = 0.2f,
    int? MaxOutputTokens = null,
    TimeSpan? Timeout = null);
