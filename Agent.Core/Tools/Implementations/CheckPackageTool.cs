using System.Text.Json;
using Agent.Core.Configuration;
using Flurl.Http;

namespace Agent.Core.Tools.Implementations;

public class CheckPackageTool : ITool
{
    private readonly AgentOptions _options;

    public CheckPackageTool(AgentOptions options)
    {
        _options = options;
    }

    public string Name => "check_package";

    public string Description => "Checks the status and details of a package by its ID.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            packageid = new { type = "string", description = "The package ID to check." }
        },
        required = new[] { "packageid" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        if (!TryGetString(parameters, "packageid", out var packageId))
            return ToolResult.Fail("Missing required parameter: packageid");

        try
        {
            var payload = new { apikey = _options.AiDevsKey, action = "check", packageid = packageId };
            var json = await (_options.HubBaseUrl + "/api/packages")
                .PostJsonAsync(payload, cancellationToken: ct)
                .ReceiveString();
            return ToolResult.Ok(json);
        }
        catch (FlurlHttpException ex)
        {
            var body = await ex.GetResponseStringAsync();
            return ToolResult.Fail($"Hub API error ({ex.StatusCode}) checking package {packageId}: {body}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Error checking package: {ex.Message}");
        }
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }
}