using Agent.Core.Configuration;
using Agent.Core.LLM.Models;
using Flurl.Http;

namespace Agent.Core.LLM;

public class OpenRouterClient : ILlmClient
{
    private readonly AgentOptions _options;

    public OpenRouterClient(AgentOptions options)
    {
        _options = options;
    }

    public async Task<ChatResponse> ChatAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ToolDefinition>? tools = null, ResponseFormat? responseFormat = null, CancellationToken ct = default)
    {
        var request = new ChatRequest
        {
            Model = _options.Model,
            Messages = messages.ToList(),
            Tools = tools?.ToList() is { Count: > 0 } toolList ? toolList : null,
            ResponseFormat = responseFormat
        };

        try
        {
            var response = await (_options.BaseUrl + "/chat/completions")
                                 .WithHeader("Authorization", $"Bearer {_options.ApiKey}")
                                 .PostJsonAsync(request, cancellationToken: ct)
                                 .ReceiveJson<ChatResponse>();

            return response;
        }
        catch (FlurlHttpException ex)
        {
            var errorBody = await ex.GetResponseStringAsync();
            throw new InvalidOperationException($"OpenRouter API error ({ex.StatusCode}): {errorBody}", ex);
        }
    }
}