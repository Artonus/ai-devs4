using System.Text.Json;

namespace Agent.Core.Tools;

internal static class ToolParameterExtensions
{
    public static bool TryGetString(this JsonElement element, string propertyName, out string? value)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }

    public static bool TryGetDouble(this JsonElement element, string propertyName, out double value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var prop))
            return false;

        return prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out value);
    }

    public static bool TryGetInt(this JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var prop))
            return false;

        return prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value);
    }
}
