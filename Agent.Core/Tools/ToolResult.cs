namespace Agent.Core.Tools;

public record ToolResult(bool Success, string Content)
{
    public static ToolResult Ok(string content)
    {
        return new ToolResult(true, content);
    }

    public static ToolResult Fail(string error)
    {
        return new ToolResult(false, error);
    }
}