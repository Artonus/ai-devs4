using Agent.Core.Agent;
using Agent.Core.Tasks.People;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Tasks.SendIt;

public class SendItTaskService
{
    private const string Model = "anthropic/claude-haiku-4-5";

    private const string SystemPrompt = """
        You are a shipping declaration agent. Your job is to fill out a hazardous materials shipping declaration
        for a specific shipment and submit it to the Hub.

        ## Shipment Details (fixed — do not change these)
        - Sender ID: 450202122
        - Route: Gdansk → Zarnowiec
        - Weight: 2800 kg
        - Cargo: kasety z paliwem do reaktora (reactor fuel cassettes)
        - PP fee: 0 (zero — find the route code for Gdansk→Zarnowiec with 0 PP fee)
        - UWAGI SPECJALNE: leave this field empty (do not fill it in)

        ## Your workflow
        1. Use `fetch_url` to fetch https://hub.ag3nts.org/dane/doc/index.md
        2. Follow every link in the index document — use `fetch_url` for text/markdown links
        3. For any image URLs (.png, .jpg, .gif, etc.) use `analyze_image` to read their content
        4. Find the declaration template document and the route table
        5. In the route table, find the route code for Gdansk→Zarnowiec where PP = 0
        6. Fill in the declaration template with the shipment details above
        7. Submit using `submit_answer` with task="sendit" and answer={"declaration": "<filled template text>"}

        ## Tools available

        ### fetch_url
        Parameter: url (string)
        Fetches a URL. Returns text content for text/markdown/html documents.
        For image URLs, automatically uses vision to transcribe the content.

        ### analyze_image
        Parameters: image_url (string), question (string, optional)
        Downloads an image and uses vision LLM to analyze it.
        Use this when you need to ask a specific question about an image.

        ### submit_answer
        Parameters: task (string), answer (string — JSON-encoded)
        Submits the final answer to the Hub.
        For this task: task="sendit", answer={"declaration": "<complete filled declaration text>"}

        ## Important guidelines
        - Fetch ALL linked documents before filling the template — you may need multiple documents
        - The route code format may look like a short alphanumeric code — check the table carefully
        - Fill EVERY field in the template with the correct values from the shipment details
        - Submit only when the declaration is complete and correct
        """;

    private readonly AgentRunner _agentRunner;

    private readonly ILogger<SendItTaskService> _logger;

    private TaskLogWriter? _logWriter;

    public SendItTaskService(AgentRunner agentRunner, ILogger<SendItTaskService> logger)
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
        Emit("// SendIt Task — starting shipping declaration agent...");

        _agentRunner.SetLogWriter(line => _logWriter?.Invoke(line));

        var result = await _agentRunner.RunAsync(
            "Fetch the shipping documentation, find the correct route code for Gdansk→Zarnowiec with 0 PP fee, fill the declaration template, and submit it.",
            SystemPrompt,
            20,
            Model,
            ct);

        if (result.LimitReached)
        {
            Emit($"WARNING: Agent hit iteration limit ({result.IterationsUsed} iterations, {result.ToolCallsCount} tool calls). Task may be incomplete.");
            _logger.LogWarning(
                "SendIt agent hit iteration limit. IterationsUsed={Iterations}, ToolCallsCount={ToolCalls}, LastResponse={Response}",
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
