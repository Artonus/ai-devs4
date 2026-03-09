using Agent.Core.LLM.Models;

namespace Agent.Core.Tools;

public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
        Console.WriteLine($"[Registry] Registered tool: {tool.Name}");
    }

    public ITool? Get(string name) => _tools.GetValueOrDefault(name);

    public IReadOnlyCollection<ITool> All => _tools.Values;

    public IEnumerable<ToolDefinition> GetDefinitions() =>
        _tools.Values.Select(t => t.ToDefinition());
}
