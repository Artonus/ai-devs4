using System.Text.Json.Serialization;

namespace Agent.Core.LLM.Models;

public class ChatRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolDefinition>? Tools { get; set; }

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseFormat? ResponseFormat { get; set; }
}

public class ResponseFormat
{
    [JsonPropertyName("type")] public string Type { get; set; } = "json_schema";

    [JsonPropertyName("json_schema")] public JsonSchemaDefinition? JsonSchema { get; set; }
}

public class JsonSchemaDefinition
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("strict")] public bool Strict { get; set; } = true;

    [JsonPropertyName("schema")] public object Schema { get; set; } = new();
}