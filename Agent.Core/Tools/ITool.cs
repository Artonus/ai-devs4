using System.Text.Json;
using Agent.Core.LLM.Models;

namespace Agent.Core.Tools;

public interface ITool
{
    string Name { get; }

    string Description { get; }

    /// <summary>
    ///     JSON Schema object describing the tool's parameters.
    /// </summary>
    object ParameterSchema { get; }

    Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default);

    ToolDefinition ToDefinition()
    {
        return new ToolDefinition
            { Function = new ToolFunction { Name = Name, Description = Description, Parameters = ParameterSchema } };
    }
}