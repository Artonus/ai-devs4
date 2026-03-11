namespace Agent.Core.LLM;

using global::Agent.Core.LLM.Models;

public interface ILlmClient
{
    Task<ChatResponse> ChatAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ToolDefinition>? tools = null, ResponseFormat? responseFormat = null, string? modelOverride = null, CancellationToken ct = default);
}
