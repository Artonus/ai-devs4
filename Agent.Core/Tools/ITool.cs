namespace Agent.Core.Tools;

using System.Text.Json;
using global::Agent.Core.LLM.Models;

public interface ITool
{
    string Name { get; }

    string Description { get; }

    /// <summary>
    ///     JSON Schema object describing the tool's parameters.
    /// </summary>
    object ParameterSchema { get; }

    Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default);

    ToolDefinition ToDefinition() => new() { Function = new ToolFunction { Name = Name, Description = Description, Parameters = ParameterSchema } };
}