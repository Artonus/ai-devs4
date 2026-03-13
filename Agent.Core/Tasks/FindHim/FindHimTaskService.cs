using Agent.Core.Agent;
using Agent.Core.Tasks.People;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Tasks.FindHim;

public class FindHimTaskService
{
    private const string Model = "openai/gpt-5-mini";

    /// <summary>
    ///     Static system prompt — kept free of dynamic values to enable prompt caching.
    ///     All runtime data (suspects, locations, power plants) is retrieved via tool calls.
    /// </summary>
    private const string SystemPrompt = """
                                        You are an investigator tasked with identifying which suspect from the S01E01 case
                                        was near a nuclear power plant. Your goal is to find that person, determine their
                                        nuclear facility access level, identify the power plant code, and submit the answer.
                                        When running tool calls, schedule all calls at the same time.

                                        ## Tools available to you

                                        ### load_suspects
                                        Call this first. Returns a JSON array of suspects with their name, surname, and birthYear.
                                        Each suspect was identified in a previous investigation as a person of interest.

                                        ### hub_api_query
                                        Queries the Hub API. Three endpoints are supported:

                                        - endpoint: "power_plants"
                                          No additional parameters needed.
                                          Returns a list of nuclear power plants with their GPS coordinates (lat/lon) and plant codes.

                                        - endpoint: "location"
                                          Parameters: name (string), surname (string)
                                          Returns the list of known geographic coordinates (lat/lon) of the given suspect.

                                        - endpoint: "accesslevel"
                                          Parameters: name (string), surname (string), birthYear (integer)
                                          Returns the nuclear facility access level for the given suspect.

                                        ### calculate_distance
                                        Parameters: lat1, lon1, lat2, lon2 (all numbers in decimal degrees)
                                        Returns { distanceKm } — the Haversine great-circle distance between two coordinates.
                                        Use this to measure how far each suspect was from each power plant.

                                        ### submit_answer
                                        Parameters: task (string), answer (string — JSON-encoded)
                                        Submits the final answer to the Hub for verification.
                                        For this task use task="findhim" and answer={"name":"...","surname":"...","accessLevel":<int>,"powerPlant":"PWR####PL"}.
                                        Call this ONLY when you are confident in all four values.

                                        ## Investigation guidelines

                                        1. Load the suspects list first.
                                        2. Fetch all power plant locations.
                                        3. For each suspect, query their locations.
                                        4. For each suspect query their access level using their name, surname , and birthYear.
                                        5. Calculate the distance between all suspects location and all power plants.
                                        6. Identify which suspect was closest to (or near) a nuclear power plant.
                                        7. Note the power plant code for the nearest plant.
                                        8. Submit your findings using submit_answer with task="findhim".

                                        Be methodical. Do not guess — all required information is available through the tools.
                                        """;

    private readonly AgentRunner _agentRunner;

    private readonly ILogger<FindHimTaskService> _logger;

    private TaskLogWriter? _logWriter;

    public FindHimTaskService(AgentRunner agentRunner, ILogger<FindHimTaskService> logger)
    {
        _agentRunner = agentRunner;
        _logger = logger;
    }

    /// <summary>Registers a delegate that receives human-readable progress lines for the UI.</summary>
    public void SetLogWriter(TaskLogWriter writer)
    {
        _logWriter = writer;
    }

    private void Emit(string line)
    {
        _logger.LogInformation("{Line}", line);
        _logWriter?.Invoke(line);
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        Emit("// FindHim Task — starting investigation agent...");

        _agentRunner.SetLogWriter(line => _logWriter?.Invoke(line));

        var result = await _agentRunner.RunAsync(
            "Begin the investigation. Find which suspect was near a nuclear power plant, determine their access level and the plant code, then submit the answer.",
            SystemPrompt,
            15,
            Model,
            ct);

        if (result.LimitReached)
        {
            Emit(
                $"WARNING: Agent hit iteration limit ({result.IterationsUsed} iterations, {result.ToolCallsCount} tool calls). Investigation may be incomplete.");
            _logger.LogWarning(
                "FindHim agent hit iteration limit. IterationsUsed={Iterations}, ToolCallsCount={ToolCalls}, LastResponse={Response}",
                result.IterationsUsed,
                result.ToolCallsCount,
                result.Response);
        }
        else
        {
            Emit($"// Agent completed in {result.IterationsUsed} iterations, {result.ToolCallsCount} tool calls.");
        }

        Emit($"// Agent response: {result.Response}");
    }
}