using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Tasks.People;

namespace Agent.Core.Tools.Implementations;

public class LoadSuspectsTool : ITool
{
    private readonly AgentOptions _options;
    private readonly PeopleTaskService _peopleTaskService;

    public LoadSuspectsTool(PeopleTaskService peopleTaskService, AgentOptions options)
    {
        _peopleTaskService = peopleTaskService;
        _options = options;
    }

    public string Name => "load_suspects";

    public string Description =>
        "Loads the list of suspects identified in the previous task. " +
        "Returns an array of objects with name, surname, and birthYear fields. " +
        "If the suspects file does not exist yet, the People pipeline runs automatically " +
        "to generate it. Call this first to know who to investigate.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new { },
        required = Array.Empty<string>()
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_options.SuspectsFilePath))
            {
                var candidates = _peopleTaskService.LoadAndFilter(_options.PeopleCsvPath);
                await _peopleTaskService.TagAndFilterAsync(candidates, ct: ct);
                // TagAndFilterAsync calls SaveSuspectsToFile internally, so the file now exists.
            }

            var json = await File.ReadAllTextAsync(_options.SuspectsFilePath, ct);
            return ToolResult.Ok(json);
        }
        catch (FileNotFoundException ex)
        {
            return ToolResult.Fail($"Required file not found: {ex.FileName}. " +
                                   $"Ensure '{_options.PeopleCsvPath}' exists and the path is correct in Agent options.");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Error loading suspects: {ex.Message}");
        }
    }
}