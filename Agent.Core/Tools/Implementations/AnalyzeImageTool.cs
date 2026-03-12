using System.Text.Json;
using Agent.Core.LLM;
using Flurl.Http;

namespace Agent.Core.Tools.Implementations;

/// <summary>
///     Downloads an image from a URL and uses vision LLM to answer a question about it.
/// </summary>
public class AnalyzeImageTool : ITool
{
    private const string VisionModel = "openai/gpt-5-mini";

    private readonly ILlmClient _llmClient;

    public AnalyzeImageTool(ILlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    public string Name => "analyze_image";

    public string Description =>
        "Downloads an image from a URL and uses a vision model to analyze it. " +
        "Use this to read text from images, tables, diagrams, or any image-based document.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            image_url = new { type = "string", description = "URL of the image to analyze." },
            question = new
            {
                type = "string",
                description =
                    "What to ask about the image. Defaults to transcribing all text content if not specified."
            }
        },
        required = new[] { "image_url" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetProperty("image_url", out var urlEl) || urlEl.ValueKind != JsonValueKind.String)
            return ToolResult.Fail("Missing required parameter: image_url");

        var url = urlEl.GetString()!;
        var question = parameters.TryGetProperty("question", out var qEl) && qEl.ValueKind == JsonValueKind.String
            ? qEl.GetString() ?? "Transcribe all text in this image exactly. Describe its full content."
            : "Transcribe all text in this image exactly. Describe its full content.";

        var cacheKey = $"{url}|{question}";
        var cached = await UrlCache.GetAsync(cacheKey, ct);
        if (cached is not null)
            return ToolResult.Ok(cached);

        try
        {
            var bytes = await url.GetBytesAsync(cancellationToken: ct);
            var extension = Path.GetExtension(new Uri(url).AbsolutePath).TrimStart('.').ToLowerInvariant();
            var mediaType = extension switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "gif" => "image/gif",
                "webp" => "image/webp",
                _ => "image/png"
            };

            var dataUri = $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
            var result = await _llmClient.DescribeImageAsync(dataUri, question, VisionModel, ct);
            await UrlCache.SetAsync(cacheKey, result, ct);
            return ToolResult.Ok(result);
        }
        catch (FlurlHttpException ex)
        {
            var body = await ex.GetResponseStringAsync();
            return ToolResult.Fail($"HTTP error ({ex.StatusCode}) downloading image: {body}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Error analyzing image: {ex.Message}");
        }
    }
}
