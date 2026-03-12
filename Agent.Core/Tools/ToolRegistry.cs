using Agent.Core.LLM.Models;

namespace Agent.Core.Tools;

public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ITool> All => _tools.Values;

    public void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
        Console.WriteLine($"[Registry] Registered tool: {tool.Name}");
    }

    public ITool? Get(string name)
    {
        return _tools.GetValueOrDefault(name);
    }

    public IEnumerable<ToolDefinition> GetDefinitions()
    {
        return _tools.Values.Select(t => t.ToDefinition());
    }
}