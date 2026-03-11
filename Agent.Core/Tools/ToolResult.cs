namespace Agent.Core.Tools;

public record ToolResult(bool Success, string Content)
{
    public static ToolResult Ok(string content) => new(true, content);

    public static ToolResult Fail(string error) => new(false, error);
}