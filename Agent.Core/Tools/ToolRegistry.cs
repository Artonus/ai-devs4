namespace Agent.Core.Tools;

using global::Agent.Core.LLM.Models;

public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ITool> All => _tools.Values;

    public void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
        Console.WriteLine($"[Registry] Registered tool: {tool.Name}");
    }

    public ITool? Get(string name) => _tools.GetValueOrDefault(name);

    public IEnumerable<ToolDefinition> GetDefinitions() => _tools.Values.Select(t => t.ToDefinition());
}