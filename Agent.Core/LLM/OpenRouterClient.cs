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

    public async Task<string> DescribeImageAsync(string imageDataUri, string question,
        string? modelOverride = null, CancellationToken ct = default)
    {
        var request = new
        {
            model = modelOverride ?? _options.Model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = question },
                        new { type = "image_url", image_url = new { url = imageDataUri } }
                    }
                }
            }
        };

        try
        {
            var response = await (_options.BaseUrl + "/chat/completions")
                .WithHeader("Authorization", $"Bearer {_options.ApiKey}")
                .PostJsonAsync(request, cancellationToken: ct)
                .ReceiveJson<VisionResponse>();

            return response.Choices[0].Message.Content ?? string.Empty;
        }
        catch (FlurlHttpException ex)
        {
            var errorBody = await ex.GetResponseStringAsync();
            throw new InvalidOperationException($"OpenRouter vision API error ({ex.StatusCode}): {errorBody}", ex);
        }
    }

    public async Task<ChatResponse> ChatAsync(IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null, ResponseFormat? responseFormat = null,
        string? modelOverride = null, CancellationToken ct = default)
    {
        var request = new ChatRequest
        {
            Model = modelOverride ?? _options.Model,
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

file class VisionResponse
{
    public List<VisionChoice> Choices { get; set; } = [];
}

file class VisionChoice
{
    public VisionMessage Message { get; set; } = new();
}

file class VisionMessage
{
    public string? Content { get; set; }
}