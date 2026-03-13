using System.Text.Json;
using Agent.Core.Configuration;
using Flurl.Http;

namespace Agent.Core.Tools.Implementations;

/// <summary>
///     Calls the Hub railway API at /verify with task="railway".
///     Automatically retries on 503 (intentional errors) with exponential backoff
///     and honours Retry-After headers for rate limiting.
/// </summary>
public class RailwayApiTool : ITool
{
    private const int MaxRetries = 8;
    private const int BaseDelayMs = 1500;

    private readonly AgentOptions _options;

    public RailwayApiTool(AgentOptions options)
    {
        _options = options;
    }

    public string Name => "railway_api";

    public string Description =>
        "Calls the railway API (task='railway') at hub.ag3nts.org/verify. " +
        "Automatically retries on 503 errors with exponential backoff and respects Retry-After rate-limit headers. " +
        "Start with answer='{\"action\":\"help\"}' to discover the full API protocol.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            answer = new
            {
                type = "string",
                description =
                    "The answer payload as a JSON string. " +
                    "Use '{\"action\":\"help\"}' first to discover the protocol, then follow instructions from the API."
            }
        },
        required = new[] { "answer" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetProperty("answer", out var answerEl) || answerEl.ValueKind != JsonValueKind.String)
            return ToolResult.Fail("Missing required parameter: answer");

        var answerStr = answerEl.GetString()!;

        object answerObj;
        try
        {
            answerObj = JsonDocument.Parse(answerStr).RootElement;
        }
        catch
        {
            answerObj = answerStr;
        }

        var payload = new { apikey = _options.AiDevsKey, task = "railway", answer = answerObj };

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var response = await (_options.HubBaseUrl + "/verify")
                    .AllowAnyHttpStatus()
                    .PostJsonAsync(payload, cancellationToken: ct);

                if (response.StatusCode is 503 or 429)
                {
                    var retryAfterHeader = response.Headers.FirstOrDefault("Retry-After");
                    int delayMs;

                    if (int.TryParse(retryAfterHeader, out var retryAfterSecs) && retryAfterSecs > 0)
                        delayMs = retryAfterSecs * 1000;
                    else
                        delayMs = (int)(BaseDelayMs * Math.Pow(2, attempt));

                    await Task.Delay(delayMs, ct);
                    continue;
                }

                var body = await response.GetStringAsync();
                return ToolResult.Ok($"HTTP {response.StatusCode}: {body}");
            }
            catch (FlurlHttpException ex)
            {
                var body = await ex.GetResponseStringAsync();
                return ToolResult.Fail($"Railway API error ({ex.StatusCode}): {body}");
            }
        }

        return ToolResult.Fail($"Railway API failed after {MaxRetries} retries (repeated 503/429).");
    }
}
