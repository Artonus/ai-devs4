namespace Agent.Core.Tools.Implementations;

using System.Text.Json;

public class FileReadTool : ITool
{
    public string Name => "file_read";

    public string Description => "Read the contents of a file at the given path.";

    public object ParameterSchema => new
                                     {
                                         type = "object",
                                         properties = new { path = new { type = "string", description = "The absolute or relative path to the file to read." } },
                                         required = new[] { "path" }
                                     };

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetProperty("path", out var pathElement))
            return ToolResult.Fail("Missing required parameter: path");

        var path = pathElement.GetString();
        if (string.IsNullOrWhiteSpace(path))
            return ToolResult.Fail("Parameter 'path' must not be empty.");

        try
        {
            var content = await File.ReadAllTextAsync(path, ct);
            return ToolResult.Ok(content);
        }
        catch (FileNotFoundException)
        {
            return ToolResult.Fail($"File not found: {path}");
        }
        catch (UnauthorizedAccessException)
        {
            return ToolResult.Fail($"Access denied: {path}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Error reading file: {ex.Message}");
        }
    }
}