using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.LLM;
using Agent.Core.LLM.Models;
using Agent.Core.Tools;

namespace Agent.Core.Agent;

public class AgentRunner
{
    private readonly ILlmClient _llmClient;
    private readonly ToolRegistry _registry;
    private readonly string _systemPrompt;

    public AgentRunner(ILlmClient llmClient, ToolRegistry registry, AgentOptions options)
    {
        _llmClient = llmClient;
        _registry = registry;
        _systemPrompt = options.SystemPrompt;
    }

    public async Task<string> RunAsync(string userInput, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.System(_systemPrompt),
            ChatMessage.User(userInput)
        };

        var tools = _registry.GetDefinitions().ToList();

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var response = await _llmClient.ChatAsync(messages, tools.Count > 0 ? tools : null, ct: ct);

            if (response.Error is not null)
                throw new InvalidOperationException($"LLM error: {response.Error.Message}");

            if (response.Choices.Count == 0)
                throw new InvalidOperationException("LLM returned no choices.");

            var choice = response.Choices[0];
            var assistantMessage = choice.Message;

            // Append assistant message to history
            messages.Add(ChatMessage.Assistant(assistantMessage.Content, assistantMessage.ToolCalls));

            // No tool calls → final answer
            if (assistantMessage.ToolCalls is not { Count: > 0 })
                return assistantMessage.Content ?? string.Empty;

            // Execute each tool call
            foreach (var toolCall in assistantMessage.ToolCalls)
            {
                var toolName = toolCall.Function.Name;
                var tool = _registry.Get(toolName);

                Console.WriteLine($"[Agent] Calling tool: {toolName}");

                if (tool is null)
                {
                    var errorMsg = $"Unknown tool: '{toolName}'";
                    Console.WriteLine($"[ERROR] {errorMsg}");
                    messages.Add(ChatMessage.Tool(toolCall.Id, $"Error: {errorMsg}"));
                    continue;
                }

                JsonElement arguments;
                try
                {
                    arguments = JsonSerializer.Deserialize<JsonElement>(toolCall.Function.Arguments);
                }
                catch (JsonException ex)
                {
                    var errorMsg = $"Invalid arguments JSON: {ex.Message}";
                    Console.WriteLine($"[ERROR] {toolName}: {errorMsg}");
                    messages.Add(ChatMessage.Tool(toolCall.Id, $"Error: {errorMsg}"));
                    continue;
                }

                var result = await tool.ExecuteAsync(arguments, ct);

                if (!result.Success)
                    Console.WriteLine($"[ERROR] {toolName}: {result.Content}");

                messages.Add(ChatMessage.Tool(toolCall.Id, result.Content));
            }
        }
    }
}
