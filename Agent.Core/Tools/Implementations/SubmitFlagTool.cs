using System.Text.Json;
using Agent.Core.Configuration;
using Flurl.Http;

namespace Agent.Core.Tools.Implementations;

/// <summary>
///     Submits a flag value to the Hub answer endpoint.
/// </summary>
public class SubmitFlagTool : ITool
{
    private readonly AgentOptions _options;

    public SubmitFlagTool(AgentOptions options)
    {
        _options = options;
    }

    public string Name => "submit_flag";

    public string Description => "Submits a flag to the Hub for verification. " +
                                 "Call this when the API returns a flag matching the pattern {{FLG:...}}.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            flag = new
            {
                type = "string",
                description = "The full flag value, e.g. \"{{FLG:XXXXXXXX}}\""
            }
        },
        required = new[] { "flag" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetProperty("flag", out var flagEl) || flagEl.ValueKind != JsonValueKind.String)
            return ToolResult.Fail("Missing required parameter: flag");

        var flag = flagEl.GetString()!;

        try
        {
            var json = await (_options.HubBaseUrl + "/answer")
                .PostUrlEncodedAsync(new { key = _options.AiDevsKey, flag }, cancellationToken: ct)
                .ReceiveString();
            return ToolResult.Ok(json);
        }
        catch (FlurlHttpException ex)
        {
            var body = await ex.GetResponseStringAsync();
            return ToolResult.Fail($"Hub answer error ({ex.StatusCode}): {body}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Error submitting flag: {ex.Message}");
        }
    }
}
