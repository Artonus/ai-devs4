using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.LLM;
using Agent.Core.LLM.Models;
using Agent.Core.Tools;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Agent;

public class AgentRunner
{
    private readonly ILlmClient _llmClient;

    private readonly ILogger<AgentRunner> _logger;

    private readonly ToolRegistry _registry;

    private readonly string _systemPrompt;

    private Action<string>? _logWriter;

    public AgentRunner(ILlmClient llmClient, ToolRegistry registry, AgentOptions options, ILogger<AgentRunner> logger)
    {
        _llmClient = llmClient;
        _registry = registry;
        _systemPrompt = options.SystemPrompt;
        _logger = logger;
    }

    /// <summary>Registers a delegate that receives human-readable progress lines for the UI.</summary>
    public void SetLogWriter(Action<string> writer)
    {
        _logWriter = writer;
    }

    private void Emit(string line)
    {
        _logger.LogInformation("{Line}", line);
        _logWriter?.Invoke(line);
    }

    /// <summary>
    ///     Runs the agentic loop with a configurable system prompt override and iteration cap.
    /// </summary>
    /// <param name="userInput">The initial user message.</param>
    /// <param name="systemPromptOverride">If provided, replaces the default system prompt from AgentOptions.</param>
    /// <param name="maxIterations">Maximum number of LLM calls before halting. Default: 15.</param>
    /// <param name="modelOverride">If provided, overrides the model from AgentOptions for this run.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<AgentRunResult> RunAsync(string userInput, string? systemPromptOverride = null,
        int maxIterations = 15, string? modelOverride = null, CancellationToken ct = default)
    {
        var effectiveSystemPrompt = systemPromptOverride ?? _systemPrompt;

        var messages = new List<ChatMessage> { ChatMessage.System(effectiveSystemPrompt), ChatMessage.User(userInput) };

        var tools = _registry.GetDefinitions().ToList();

        var iterations = 0;
        var toolCallsCount = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            iterations++;

            // Warn at 80% of the iteration limit
            if (iterations == (int)(maxIterations * 0.8))
            {
                var warning = $"[Agent] Approaching iteration limit: {iterations}/{maxIterations}";
                _logger.LogWarning("{Warning}", warning);
                _logWriter?.Invoke($"WARNING: {warning}");
            }

            // Hard limit reached — return without a natural finish
            if (iterations > maxIterations)
            {
                var warning = $"[Agent] Iteration limit reached ({maxIterations}). Stopping.";
                _logger.LogWarning("{Warning}", warning);
                _logWriter?.Invoke($"WARNING: {warning}");
                var lastContent = messages.LastOrDefault(m => m.Role == "assistant")?.Content ?? string.Empty;
                return new AgentRunResult(false, lastContent, iterations - 1, true, toolCallsCount);
            }

            var response = await _llmClient.ChatAsync(messages, tools.Count > 0 ? tools : null,
                modelOverride: modelOverride, ct: ct);

            if (response.Error is not null)
                throw new InvalidOperationException($"LLM error: {response.Error.Message}");

            if (response.Choices.Count == 0)
                throw new InvalidOperationException("LLM returned no choices.");

            var choice = response.Choices[0];
            var assistantMessage = choice.Message;

            // Append assistant message to history
            messages.Add(ChatMessage.Assistant(assistantMessage.Content, assistantMessage.ToolCalls));

            // Emit iteration summary to UI
            var hasToolCalls = assistantMessage.ToolCalls is { Count: > 0 };
            if (!string.IsNullOrWhiteSpace(assistantMessage.Content))
                Emit($"  [LLM #{iterations}] {assistantMessage.Content}");
            if (hasToolCalls)
            {
                var toolNames = string.Join(", ", assistantMessage.ToolCalls!.Select(t => t.Function.Name));
                Emit($"  [LLM #{iterations}] tools → {toolNames}");
            }

            // No tool calls → final answer
            if (!hasToolCalls)
                return new AgentRunResult(true, assistantMessage.Content ?? string.Empty, iterations, false,
                    toolCallsCount);

            // Execute each tool call
            foreach (var toolCall in assistantMessage.ToolCalls!)
            {
                toolCallsCount++;
                var toolName = toolCall.Function.Name;
                var tool = _registry.Get(toolName);

                Emit($"  [Tool] → {toolName}");

                if (tool is null)
                {
                    var errorMsg = $"Unknown tool: '{toolName}'";
                    _logger.LogError("{ErrorMsg}", errorMsg);
                    Emit($"  ERROR: {errorMsg}");
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
                    var errorMsg = $"Invalid arguments JSON for {toolName}: {ex.Message}";
                    _logger.LogError("{ErrorMsg}", errorMsg);
                    Emit($"  ERROR: {errorMsg}");
                    messages.Add(ChatMessage.Tool(toolCall.Id, $"Error: {errorMsg}"));
                    continue;
                }

                var result = await tool.ExecuteAsync(arguments, ct);

                if (!result.Success)
                {
                    _logger.LogError("[Tool] {ToolName} failed: {Content}", toolName, result.Content);
                    Emit($"  ERROR: {toolName}: {result.Content}");
                }

                messages.Add(ChatMessage.Tool(toolCall.Id, result.Content));
            }
        }
    }
}