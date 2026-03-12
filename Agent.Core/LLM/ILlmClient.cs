using Agent.Core.LLM.Models;

namespace Agent.Core.LLM;

public interface ILlmClient
{
    Task<ChatResponse> ChatAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ToolDefinition>? tools = null,
        ResponseFormat? responseFormat = null, string? modelOverride = null, CancellationToken ct = default);

    Task<string> DescribeImageAsync(string imageDataUri, string question,
        string? modelOverride = null, CancellationToken ct = default);
}