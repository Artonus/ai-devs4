using System.Text.Json;
using Agent.Core.Tools;
using Agent.Core.Utils;

namespace Agent.Core.Tools.Implementations;

public class CalculateDistanceTool : ITool
{
    public string Name => "calculate_distance";

    public string Description =>
        "Calculates the great-circle distance in kilometres between two geographic coordinates " +
        "using the Haversine formula. Use this to determine how far a suspect's last known " +
        "location is from a nuclear power plant.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            lat1 = new { type = "number", description = "Latitude of point 1 in decimal degrees." },
            lon1 = new { type = "number", description = "Longitude of point 1 in decimal degrees." },
            lat2 = new { type = "number", description = "Latitude of point 2 in decimal degrees." },
            lon2 = new { type = "number", description = "Longitude of point 2 in decimal degrees." }
        },
        required = new[] { "lat1", "lon1", "lat2", "lon2" }
    };

    public Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetDouble("lat1", out var lat1))
            return Task.FromResult(ToolResult.Fail("Missing or invalid parameter: lat1"));
        if (!parameters.TryGetDouble("lon1", out var lon1))
            return Task.FromResult(ToolResult.Fail("Missing or invalid parameter: lon1"));
        if (!parameters.TryGetDouble("lat2", out var lat2))
            return Task.FromResult(ToolResult.Fail("Missing or invalid parameter: lat2"));
        if (!parameters.TryGetDouble("lon2", out var lon2))
            return Task.FromResult(ToolResult.Fail("Missing or invalid parameter: lon2"));

        var distanceKm = GeoCalculator.HaversineDistanceKm(lat1, lon1, lat2, lon2);

        var result = JsonSerializer.Serialize(new { distanceKm = Math.Round(distanceKm, 3) });
        return Task.FromResult(ToolResult.Ok(result));
    }
}