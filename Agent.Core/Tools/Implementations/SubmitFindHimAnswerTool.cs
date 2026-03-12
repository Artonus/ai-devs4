using System.Text.Json;
using Agent.Core.Configuration;
using Flurl.Http;

namespace Agent.Core.Tools.Implementations;

/// <summary>
///     Submits the final FindHim investigation result to the Hub for verification.
/// </summary>
public class SubmitFindHimAnswerTool : ITool
{
    private readonly AgentOptions _options;

    public SubmitFindHimAnswerTool(AgentOptions options)
    {
        _options = options;
    }

    public string Name => "submit_findhim_answer";

    public string Description => "Submits the final answer for the FindHim task to the Hub for verification. " +
                                 "Call this ONLY when you are confident you have identified the correct suspect, " +
                                 "their access level, and the power plant code. " +
                                 "The power plant code format is PWR####PL (e.g. PWR1234PL).";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            name = new { type = "string", description = "First name of the suspect." },
            surname = new { type = "string", description = "Surname of the suspect." },
            accessLevel = new { type = "integer", description = "The nuclear facility access level of the suspect." },
            powerPlant = new
            {
                type = "string",
                description =
                    "The code of the nuclear power plant nearest to the suspect's last known location. Format: PWR####PL."
            }
        },
        required = new[] { "name", "surname", "accessLevel", "powerPlant" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        if (!TryGetString(parameters, "name", out var name))
            return ToolResult.Fail("Missing required parameter: name");
        if (!TryGetString(parameters, "surname", out var surname))
            return ToolResult.Fail("Missing required parameter: surname");
        if (!parameters.TryGetProperty("accessLevel", out var accessLevelEl) ||
            accessLevelEl.ValueKind != JsonValueKind.Number)
            return ToolResult.Fail("Missing required parameter: accessLevel (integer)");
        if (!TryGetString(parameters, "powerPlant", out var powerPlant))
            return ToolResult.Fail("Missing required parameter: powerPlant");

        var accessLevel = accessLevelEl.GetInt32();

        var payload = new
        {
            apikey = _options.AiDevsKey,
            task = "findhim",
            answer = new
            {
                name,
                surname,
                accessLevel,
                powerPlant
            }
        };

        try
        {
            var json = await (_options.HubBaseUrl + "/verify")
                .PostJsonAsync(payload, cancellationToken: ct)
                .ReceiveString();
            return ToolResult.Ok(json);
        }
        catch (FlurlHttpException ex)
        {
            var body = await ex.GetResponseStringAsync();
            return ToolResult.Fail($"Hub API error ({ex.StatusCode}) submitting answer: {body}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Error submitting answer: {ex.Message}");
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