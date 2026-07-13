using OpenAI.Chat;
using Microsoft.Extensions.Logging;
using System.ClientModel;
using System.Text.Json;

namespace Fidalgo.Shared.Tools;

public class DelegateTool
{
    private readonly ChatClient _chatClient;
    private readonly IBrowserFetchTool _browserFetchTool;
    private readonly MarkdownFetchTool _markdownFetchTool;
    private readonly ILogger<DelegateTool> _logger;

    public DelegateTool(ChatClient chatClient, IBrowserFetchTool browserFetchTool, MarkdownFetchTool markdownFetchTool, ILogger<DelegateTool> logger)
    {
        _chatClient = chatClient;
        _browserFetchTool = browserFetchTool;
        _markdownFetchTool = markdownFetchTool;
        _logger = logger;
    }

    public async Task<string> DelegateAsync(string task, string content, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Delegate task: {Task}", task);
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage($"You are a helpful sub-agent. Complete the following task using the provided content. Be concise and thorough.\n\nTask: {task}"),
            new UserChatMessage(content),
        };

        var toolFetch = ChatTool.CreateFunctionTool(
            functionName: "playwright_fetch",
            functionDescription: "Fetch a web page and return sanitized HTML content",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"url":{"type":"string","description":"The URL to fetch"}},"required":["url"]}"""));

        var toolMarkdownFetch = ChatTool.CreateFunctionTool(
            functionName: "markdown_fetch",
            functionDescription: "Fetch a web page and convert it to markdown",
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"url":{"type":"string","description":"The URL to fetch"}},"required":["url"]}"""));

        var options = new ChatCompletionOptions
        {
            Tools = { toolFetch, toolMarkdownFetch }
        };

        try
        {
            bool requiresAction;
            do
            {
                requiresAction = false;
                var completion = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
                
                if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
                {
                    messages.Add(new AssistantChatMessage(completion.Value));
                    foreach (var toolCall in completion.Value.ToolCalls)
                    {
                        if (toolCall.FunctionName == "playwright_fetch")
                        {
                            using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
                            string url = doc.RootElement.GetProperty("url").GetString() ?? "";
                            var result = await _browserFetchTool.FetchAsync(new Models.FetchRequest(url), cancellationToken);
                            messages.Add(new ToolChatMessage(toolCall.Id, result.Error ?? result.Content));
                        }
                        else if (toolCall.FunctionName == "markdown_fetch")
                        {
                            using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
                            string url = doc.RootElement.GetProperty("url").GetString() ?? "";
                            var result = await _markdownFetchTool.FetchAsync(url, cancellationToken);
                            messages.Add(new ToolChatMessage(toolCall.Id, result));
                        }
                    }
                    requiresAction = true;
                }
                else
                {
                    return completion.Value.Content[0].Text;
                }
            } while (requiresAction);

            return "Failed to complete task.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delegate task failed: {Task}", task);
            return $"Sub-agent failed: {ex.Message}";
        }
    }
}