using System.Text.Json;
using System.Text.Json.Nodes;
using Agent.Core.Configuration;
using Agent.Core.Tools;
using Flurl.Http;

namespace Agent.Core.Tools.Implementations;

/// <summary>
///     Multi-endpoint tool that queries the Hub API for power plant locations,
///     suspect last-known location, and access level data.
/// </summary>
public class HubApiQueryTool : ITool
{
    private readonly AgentOptions _options;

    public HubApiQueryTool(AgentOptions options)
    {
        _options = options;
    }

    public string Name => "hub_api_query";

    public string Description => "Queries the Hub API. Supported endpoints:\n" +
                                 "- power_plants: fetches the list of nuclear power plants with their coordinates and codes. No extra params.\n" +
                                 "- location: fetches the last known geographic location (lat/lon) of a suspect. Requires name and surname.\n" +
                                 "- accesslevel: fetches the nuclear facility access level of a suspect. Requires name, surname, and birthYear.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            endpoint = new
            {
                type = "string", @enum = new[] { "power_plants", "location", "accesslevel" },
                description = "Which Hub API endpoint to call."
            },
            name = new
            {
                type = "string",
                description = "First name of the suspect. Required for 'location' and 'accesslevel' endpoints."
            },
            surname = new
            {
                type = "string",
                description = "Surname of the suspect. Required for 'location' and 'accesslevel' endpoints."
            },
            birthYear = new
                { type = "integer", description = "Birth year of the suspect. Required for 'accesslevel' endpoint." }
        },
        required = new[] { "endpoint" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetProperty("endpoint", out var endpointEl))
            return ToolResult.Fail("Missing required parameter: endpoint");

        var endpoint = endpointEl.GetString();

        return endpoint switch
        {
            "power_plants" => await GetPowerPlantsAsync(ct),
            "location" => await GetLocationAsync(parameters, ct),
            "accesslevel" => await GetAccessLevelAsync(parameters, ct),
            _ => ToolResult.Fail($"Unknown endpoint: '{endpoint}'. Valid values: power_plants, location, accesslevel")
        };
    }

    private async Task<ToolResult> GetPowerPlantsAsync(CancellationToken ct)
    {
        try
        {
            var url = $"{_options.HubBaseUrl}/data/{_options.AiDevsKey}/findhim_locations.json";
            var json = await url.GetStringAsync(cancellationToken: ct);
            return ToolResult.Ok(json);
        }
        catch (FlurlHttpException ex)
        {
            var body = await ex.GetResponseStringAsync();
            return ToolResult.Fail($"Hub API error ({ex.StatusCode}) fetching power plants: {body}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Error fetching power plants: {ex.Message}");
        }
    }

    private async Task<ToolResult> GetLocationAsync(JsonElement parameters, CancellationToken ct)
    {
        if (!parameters.TryGetString("name", out var name))
            return ToolResult.Fail("Missing required parameter 'name' for endpoint 'location'.");
        if (!parameters.TryGetString("surname", out var surname))
            return ToolResult.Fail("Missing required parameter 'surname' for endpoint 'location'.");

        try
        {
            var payload = new { apikey = _options.AiDevsKey, name, surname };
            var json = await (_options.HubBaseUrl + "/api/location")
                .PostJsonAsync(payload, cancellationToken: ct)
                .ReceiveString();

            await AppendLocationToFileAsync(name!, surname!, json, ct);

            return ToolResult.Ok(json);
        }
        catch (FlurlHttpException ex)
        {
            var body = await ex.GetResponseStringAsync();
            return ToolResult.Fail($"Hub API error ({ex.StatusCode}) fetching location for {name} {surname}: {body}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Error fetching location: {ex.Message}");
        }
    }

    private async Task AppendLocationToFileAsync(string name, string surname, string responseJson, CancellationToken ct)
    {
        var path = _options.SuspectLocationsFilePath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Load existing entries or start fresh
        JsonObject entries;
        if (File.Exists(path))
        {
            var existing = await File.ReadAllTextAsync(path, ct);
            entries = JsonNode.Parse(existing)?.AsObject() ?? new JsonObject();
        }
        else
        {
            entries = new JsonObject();
        }

        // Key by "Name Surname" — overwrites if called again for the same person
        var key = $"{name} {surname}";
        entries[key] = JsonNode.Parse(responseJson);

        await File.WriteAllTextAsync(path, entries.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            ct);
    }

    private async Task<ToolResult> GetAccessLevelAsync(JsonElement parameters, CancellationToken ct)
    {
        if (!parameters.TryGetString("name", out var name))
            return ToolResult.Fail("Missing required parameter 'name' for endpoint 'accesslevel'.");
        if (!parameters.TryGetString("surname", out var surname))
            return ToolResult.Fail("Missing required parameter 'surname' for endpoint 'accesslevel'.");
        if (!parameters.TryGetProperty("birthYear", out var birthYearEl) ||
            birthYearEl.ValueKind != JsonValueKind.Number)
            return ToolResult.Fail("Missing required parameter 'birthYear' (integer) for endpoint 'accesslevel'.");

        var birthYear = birthYearEl.GetInt32();

        try
        {
            var payload = new
            {
                apikey = _options.AiDevsKey,
                name,
                surname,
                birthYear
            };
            var json = await (_options.HubBaseUrl + "/api/accesslevel")
                .PostJsonAsync(payload, cancellationToken: ct)
                .ReceiveString();
            return ToolResult.Ok(json);
        }
        catch (FlurlHttpException ex)
        {
            var body = await ex.GetResponseStringAsync();
            return ToolResult.Fail(
                $"Hub API error ({ex.StatusCode}) fetching access level for {name} {surname}: {body}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Error fetching access level: {ex.Message}");
        }
    }

}