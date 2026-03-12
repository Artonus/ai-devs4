using System.Text.Json;
using Agent.Core.Hub;

namespace Agent.Core.Tools.Implementations;

/// <summary>
///     Generic tool that submits an answer to the Hub for any task.
/// </summary>
public class SubmitAnswerTool : ITool
{
    private readonly HubClient _hubClient;

    public SubmitAnswerTool(HubClient hubClient)
    {
        _hubClient = hubClient;
    }

    public string Name => "submit_answer";

    public string Description =>
        "Submits the final answer to the Hub for verification. " +
        "Use this when you have gathered all required information and are ready to submit.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            task = new { type = "string", description = "The task name (e.g. 'sendit')." },
            answer = new
            {
                type = "string",
                description = "The answer as a JSON string or plain string to submit to the Hub."
            }
        },
        required = new[] { "task", "answer" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetProperty("task", out var taskEl) || taskEl.ValueKind != JsonValueKind.String)
            return ToolResult.Fail("Missing required parameter: task");
        if (!parameters.TryGetProperty("answer", out var answerEl) || answerEl.ValueKind != JsonValueKind.String)
            return ToolResult.Fail("Missing required parameter: answer");

        var task = taskEl.GetString()!;
        var answerStr = answerEl.GetString()!;

        object answerObj;
        try
        {
            answerObj = JsonDocument.Parse(answerStr).RootElement;
        }
        catch
        {
            answerObj = answerStr;
        }

        try
        {
            var response = await _hubClient.CallVerify(task, answerObj, ct);
            if (response.Code < 0)
                return ToolResult.Fail($"Hub rejected the answer (code: {response.Code}): {response.Message}");
            return ToolResult.Ok($"Code: {response.Code}, Message: {response.Message}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Error submitting answer: {ex.Message}");
        }
    }
}
