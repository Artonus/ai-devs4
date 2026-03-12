namespace Agent.Core.Agent;

/// <summary>
///     Rich result returned by <see cref="AgentRunner.RunAsync" /> that includes iteration and tool-call telemetry.
/// </summary>
public record AgentRunResult(
    bool Success,
    string Response,
    int IterationsUsed,
    bool LimitReached,
    int ToolCallsCount);