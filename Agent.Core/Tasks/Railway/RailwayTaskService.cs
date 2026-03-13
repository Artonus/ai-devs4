using Agent.Core.Agent;
using Agent.Core.Tasks.People;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Tasks.Railway;

public class RailwayTaskService
{
    private const string Model = "openai/gpt-5-mini";

    private const string SystemPrompt = """
        You are a railway control agent. Your mission is to activate railway route X-01 using the railway API.

        ## How the API works
        The railway API is self-documenting. Always start by calling railway_api with answer='{"action":"help"}'
        to retrieve the full protocol and available actions.

        ## Your workflow
        1. Call railway_api with answer='{"action":"help"}' to discover available actions and the protocol.
        2. Read the help response carefully — it will tell you what actions exist and what parameters they need.
        3. Follow the protocol step by step to activate route X-01.
        4. If a step returns an error, read the error message and adjust accordingly.
        5. Repeat until route X-01 is confirmed as activated.

        ## Thinking out loud
        Before every tool call, write a short sentence explaining what you are about to do and why.
        After receiving a tool result, summarise what you learned in one or two sentences before deciding the next step.

        ## Important behaviour
        - The API may return 503 errors intentionally — the tool handles retries automatically, you don't need to worry about this.
        - The API enforces rate limits via HTTP headers — the tool respects them automatically.
        - Never skip the help step — the API protocol may change, always read it first.
        - Always pass the full required payload as a JSON string in the answer parameter.

        ## Flags
        If the API returns a flag matching the pattern {{FLG:...}}, call `submit_flag` with the full flag value immediately.

        ## Tools available
        ### railway_api
        Parameter: answer (string — JSON-encoded payload, e.g. '{"action":"help"}')
        Calls the railway API. Retries 503s automatically. Respects rate-limit headers.

        ### submit_flag
        Parameter: flag (string — the full flag value, e.g. "{{FLG:XXXXXXXX}}")
        Submits a flag to the Hub for verification.
        """;

    private readonly AgentRunner _agentRunner;
    private readonly ILogger<RailwayTaskService> _logger;
    private TaskLogWriter? _logWriter;

    public RailwayTaskService(AgentRunner agentRunner, ILogger<RailwayTaskService> logger)
    {
        _agentRunner = agentRunner;
        _logger = logger;
    }

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
        Emit("// Railway Task — starting railway control agent...");

        _agentRunner.SetLogWriter(line => _logWriter?.Invoke(line));

        var result = await _agentRunner.RunAsync(
            "Discover the railway API protocol and activate route X-01.",
            SystemPrompt,
            25,
            Model,
            ct);

        if (result.LimitReached)
        {
            Emit($"WARNING: Agent hit iteration limit ({result.IterationsUsed} iterations, {result.ToolCallsCount} tool calls). Task may be incomplete.");
            _logger.LogWarning(
                "Railway agent hit iteration limit. IterationsUsed={Iterations}, ToolCallsCount={ToolCalls}, LastResponse={Response}",
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
