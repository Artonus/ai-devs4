using Agent.Core.LLM.Models;

namespace Agent.Core.LLM;

public interface ILlmClient
{
    Task<ChatResponse> ChatAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ToolDefinition>? tools = null, ResponseFormat? responseFormat = null, CancellationToken ct = default);
}