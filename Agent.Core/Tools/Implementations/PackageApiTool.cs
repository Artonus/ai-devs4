using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Tools;
using Flurl.Http;

namespace Agent.Core.Tools.Implementations;

/// <summary>
///     Multi-action tool for the packages API. Supports checking package status
///     and redirecting packages to a new destination.
/// </summary>
public class PackageApiTool : ITool
{
    private readonly AgentOptions _options;

    public PackageApiTool(AgentOptions options)
    {
        _options = options;
    }

    public string Name => "package_api";

    public string Description => "Interacts with the packages API. Supported actions:\n" +
                                 "- check: returns the status and details of a package. Requires packageid.\n" +
                                 "- redirect: redirects a package to a new destination. Requires packageid, destination, and code.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            action = new
            {
                type = "string",
                @enum = new[] { "check", "redirect" },
                description = "The action to perform."
            },
            packageid = new { type = "string", description = "The package ID. Required for both actions." },
            destination = new
            {
                type = "string",
                description = "The destination facility code. Required for 'redirect'."
            },
            code = new { type = "string", description = "Authorization code for the redirect. Required for 'redirect'." }
        },
        required = new[] { "action", "packageid" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetString("action", out var action))
            return ToolResult.Fail("Missing required parameter: action");
        if (!parameters.TryGetString("packageid", out var packageId))
            return ToolResult.Fail("Missing required parameter: packageid");

        return action switch
        {
            "check" => await CheckPackageAsync(packageId!, ct),
            "redirect" => await RedirectPackageAsync(parameters, packageId!, ct),
            _ => ToolResult.Fail($"Unknown action: '{action}'. Valid values: check, redirect")
        };
    }

    private async Task<ToolResult> CheckPackageAsync(string packageId, CancellationToken ct)
    {
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

    private async Task<ToolResult> RedirectPackageAsync(JsonElement parameters, string packageId, CancellationToken ct)
    {
        if (!parameters.TryGetString("destination", out var destination))
            return ToolResult.Fail("Missing required parameter 'destination' for action 'redirect'.");
        if (!parameters.TryGetString("code", out var code))
            return ToolResult.Fail("Missing required parameter 'code' for action 'redirect'.");

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
}
