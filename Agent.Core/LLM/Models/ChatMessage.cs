using System.Text.Json.Serialization;

namespace Agent.Core.LLM.Models;

public class ChatMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolCall>? ToolCalls { get; set; }

    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; set; }

    // Convenience factory methods
    public static ChatMessage System(string content)
    {
        return new ChatMessage { Role = "system", Content = content };
    }

    public static ChatMessage User(string content)
    {
        return new ChatMessage { Role = "user", Content = content };
    }

    public static ChatMessage Assistant(string? content, List<ToolCall>? toolCalls = null)
    {
        return new ChatMessage { Role = "assistant", Content = content, ToolCalls = toolCalls };
    }

    public static ChatMessage Tool(string toolCallId, string content)
    {
        return new ChatMessage { Role = "tool", ToolCallId = toolCallId, Content = content };
    }
}