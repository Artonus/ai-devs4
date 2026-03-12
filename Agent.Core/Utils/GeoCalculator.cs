namespace Agent.Core.Utils;

/// <summary>
///     Geographic distance calculations.
/// </summary>
public static class GeoCalculator
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    ///     Calculates the great-circle distance between two points on Earth using the Haversine formula.
    /// </summary>
    /// <param name="lat1">Latitude of point 1 in decimal degrees.</param>
    /// <param name="lon1">Longitude of point 1 in decimal degrees.</param>
    /// <param name="lat2">Latitude of point 2 in decimal degrees.</param>
    /// <param name="lon2">Longitude of point 2 in decimal degrees.</param>
    /// <returns>Distance in kilometres.</returns>
    public static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}