using System.Text.Json;
using Agent.Core.Configuration;
using Flurl.Http;

namespace Agent.Core.Tools.Implementations;

public class RedirectPackageTool : ITool
{
    private readonly AgentOptions _options;

    public RedirectPackageTool(AgentOptions options)
    {
        _options = options;
    }

    public string Name => "redirect_package";

    public string Description => "Redirects a package to a new destination. Returns a confirmation field on success.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            packageid = new { type = "string", description = "The package ID to redirect." },
            destination = new { type = "string", description = "The destination facility code." },
            code = new { type = "string", description = "Authorization code for the redirect." }
        },
        required = new[] { "packageid", "destination", "code" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        if (!TryGetString(parameters, "packageid", out var packageId))
            return ToolResult.Fail("Missing required parameter: packageid");
        if (!TryGetString(parameters, "destination", out var destination))
            return ToolResult.Fail("Missing required parameter: destination");
        if (!TryGetString(parameters, "code", out var code))
            return ToolResult.Fail("Missing required parameter: code");

        try
        {
            var payload = new
                { apikey = _options.AiDevsKey, action = "redirect", packageid = packageId, destination, code };
            var json = await (_options.HubBaseUrl + "/api/packages")
                .PostJsonAsync(payload, cancellationToken: ct)
                .ReceiveString();
            return ToolResult.Ok(json);
        }
        catch (FlurlHttpException ex)
        {
            var body = await ex.GetResponseStringAsync();
            return ToolResult.Fail($"Hub API error ({ex.StatusCode}) redirecting package {packageId}: {body}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Error redirecting package: {ex.Message}");
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