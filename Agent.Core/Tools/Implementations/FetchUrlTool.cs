using System.Text.Json;
using Agent.Core.LLM;
using Flurl.Http;

namespace Agent.Core.Tools.Implementations;

/// <summary>
///     Fetches a URL and returns its content as text.
///     For image URLs, uses vision LLM to transcribe/describe the content.
///     An optional <c>question</c> parameter guides the vision analysis.
/// </summary>
public class FetchUrlTool : ITool
{
    private const string VisionModel = "openai/gpt-5-mini";
    private const string DefaultImagePrompt = "Transcribe all text in this image exactly. Describe its full content.";

    private readonly ILlmClient _llmClient;

    public FetchUrlTool(ILlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    public string Name => "fetch_url";

    public string Description =>
        "Fetches a URL and returns its content. Text/HTML/Markdown URLs return raw content. " +
        "Image URLs are automatically analyzed via vision LLM and their text content is returned. " +
        "Supply an optional 'question' to guide what the vision model focuses on when analyzing an image.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            url = new { type = "string", description = "The URL to fetch." },
            question = new
            {
                type = "string",
                description =
                    "Optional. For image URLs: what to ask the vision model. Defaults to transcribing all text."
            }
        },
        required = new[] { "url" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetProperty("url", out var urlEl) || urlEl.ValueKind != JsonValueKind.String)
            return ToolResult.Fail("Missing required parameter: url");

        var url = urlEl.GetString()!;
        var question = parameters.TryGetProperty("question", out var qEl) && qEl.ValueKind == JsonValueKind.String
            ? qEl.GetString() ?? DefaultImagePrompt
            : DefaultImagePrompt;

        var cacheKey = question == DefaultImagePrompt ? url : $"{url}|{question}";
        var cached = await UrlCache.GetAsync(cacheKey, ct);
        if (cached is not null)
            return ToolResult.Ok(cached);

        try
        {
            var response = await url.GetAsync(cancellationToken: ct);
            var contentType = response.Headers.FirstOrDefault("Content-Type") ?? string.Empty;

            string result;
            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await response.GetBytesAsync();
                var mediaType = contentType.Split(';')[0].Trim();
                var dataUri = $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
                result = await _llmClient.DescribeImageAsync(dataUri, question, VisionModel, ct);
            }
            else
            {
                result = await response.GetStringAsync();
            }

            await UrlCache.SetAsync(cacheKey, result, ct);
            return ToolResult.Ok(result);
        }
        catch (FlurlHttpException ex)
        {
            var body = await ex.GetResponseStringAsync();
            return ToolResult.Fail($"HTTP error ({ex.StatusCode}) fetching {url}: {body}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Error fetching {url}: {ex.Message}");
        }
    }
}
