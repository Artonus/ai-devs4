# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is an **AI-powered agent system** built with C# and .NET 10.0. It implements an agentic AI architecture where an LLM orchestrates multi-step tasks by calling tools, designed to solve AI development challenges (ai-devs).

**Key Stack:**
- .NET 10.0 (C#)
- ASP.NET Core Blazor (Web UI)
- OpenRouter API (LLM provider)
- Flurl.Http (HTTP client)

## Architecture Overview

### Core Pattern: Agent Loop with Function Calling

The project uses an **agentic function calling pattern**:

```
User Input → LLM (with system prompt) → Decides if needs tools?
  ├─ Yes: Parse tool calls → Execute tools → Add results to history
  │       Loop back to LLM with updated context
  └─ No: Return final response to user
```

### Project Structure

```
Agent.Core/
├── Agent/
│   ├── AgentRunner.cs       # Main agentic loop (iteration limits, tool execution)
│   └── AgentRunResult.cs    # Rich result type (success, iterations, limits hit)
├── LLM/
│   ├── OpenRouterClient.cs  # Calls OpenRouter API
│   ├── ILlmClient.cs        # Interface for LLM providers
│   └── Models/              # ChatMessage, ChatRequest, ChatResponse, ToolCall, ToolDefinition
├── Tools/
│   ├── ITool.cs             # Tool interface (Name, Description, ParameterSchema, ExecuteAsync)
│   ├── ToolRegistry.cs      # Registry for available tools
│   ├── ToolResult.cs        # Tool execution result
│   └── Implementations/     # Concrete tool implementations
│       ├── FileReadTool.cs           # Read files from disk
│       ├── LoadSuspectsTool.cs       # Load suspects from JSON
│       ├── HubApiQueryTool.cs        # Query Hub API (power plants, location, access level)
│       ├── CalculateDistanceTool.cs  # Haversine distance calculation
│       └── SubmitFindHimAnswerTool.cs# Submit final answer to Hub
├── Tasks/
│   ├── People/PeopleTaskService.cs   # Task: Find suspects (S01E01)
│   └── FindHim/FindHimTaskService.cs # Task: Find suspect near power plant (S01E02)
├── Configuration/
│   └── AgentOptions.cs      # Settings (API keys, URLs, models, system prompt)
└── Utils/
    └── GeoCalculator.cs     # Geo math (Haversine distance)

Agent.UI/
├── Program.cs               # DI setup, registers tools in ToolRegistry
├── Components/Pages/
│   └── TaskRunner.razor     # Web UI for running tasks
└── Services/
    └── TaskLogService.cs    # Real-time UI updates during task execution
```

### Data Flow

1. User selects a task in Blazor UI (TaskRunner.razor)
2. Task service (e.g., FindHimTaskService) calls `AgentRunner.RunAsync()`
3. AgentRunner implements the agentic loop:
   - Calls OpenRouter API with system prompt and message history
   - Parses tool calls from LLM response
   - Executes tools via ToolRegistry
   - Appends results to message history and loops
   - Returns rich result (AgentRunResult) with iteration metadata
4. UI displays progress in real-time via TaskLogService

### Key Architectural Decisions

- **Iteration Limits**: AgentRunner has `maxIterations` (default 15) to prevent infinite loops
- **Rich Result Type**: AgentRunResult returns not just the response but iterations used, limit reached flag, tool call count
- **Tool Registry Pattern**: Tools are pluggable; new tools added in Program.cs DI setup
- **Static System Prompts**: Prompts are const strings in task services (enables prompt caching for efficiency)
- **Task Services**: High-level orchestration layer; each task has its own service (People, FindHim) with custom system prompts

## Common Development Tasks

### Build
```bash
dotnet build
```

### Run the Web App
```bash
dotnet run --project Agent.UI/Agent.UI.csproj
```
- Defaults to https://localhost:7000 (check launchSettings.json)
- Requires API keys set up via user secrets (see Configuration below)

### Configure API Keys
The project uses user secrets for sensitive values:
```bash
# Set API key (AiDevs Hub)
dotnet user-secrets set "AgentOptions:AiDevsKey" "your-key-here" --project Agent.UI

# Set API key (OpenRouter, if needed)
dotnet user-secrets set "AgentOptions:ApiKey" "your-openrouter-key" --project Agent.UI

# List all configured secrets
dotnet user-secrets list --project Agent.UI
```

Configuration is read from `appsettings.json` and `appsettings.Development.json`, then merged with user secrets.

### Adding a New Tool

1. Create a class implementing `ITool`:
   ```csharp
   public class MyNewTool : ITool
   {
       public string Name => "my_tool";
       public string Description => "Does something useful";
       public object ParameterSchema => new { /* JSON schema */ };
       public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
       {
           // Parse parameters and execute
       }
   }
   ```

2. Register in `Agent.UI/Program.cs` ToolRegistry setup:
   ```csharp
   registry.Register(new MyNewTool());
   ```

3. The tool is now available to agents automatically.

### Adding a New Task

1. Create a `TaskService` in `Agent.Core/Tasks/MyTask/MyTaskService.cs`:
   - Include a `const string SystemPrompt` with the agent's role and guidelines
   - Implement `async Task<string> RunAsync(...)` calling `AgentRunner.RunAsync()`
   - Use `_taskLogWriter` to emit progress to UI

2. Register in `Agent.UI/Program.cs`:
   ```csharp
   builder.Services.AddSingleton<MyTaskService>();
   ```

3. Add UI entry in `Agent.UI/Components/Pages/TaskRunner.razor`:
   - Add to `_tasks` array
   - Add `RunMyTaskAsync()` method

## Understanding the Agent Loop

The **AgentRunner** is the core engine. Here's how it works:

- **Input**: User message, system prompt, tools, max iterations
- **Loop**:
  1. Send messages + tools to LLM
  2. Parse response:
     - If LLM calls tools → execute each tool, append results, loop back
     - If LLM provides final answer → return result
  3. Track iterations (log warning at 80%, error at max)
- **Output**: AgentRunResult with success flag, response text, iteration count, limit-reached flag, tool count

Key methods:
- `RunAsync(userInput, systemPromptOverride?, maxIterations = 15, modelOverride?, ct)` → Returns AgentRunResult
- `SetLogWriter(Action<string>)` → Register callback for UI progress updates

## Important Notes

### Tool Parameter Parsing

Tools receive a `JsonElement` of parameters. Use `JsonDocument.Parse()` or extension methods like `.GetProperty()` to extract values:
```csharp
var name = parameters.GetProperty("name").GetString();
var count = parameters.GetProperty("count").GetInt32();
```

### ToolResult

Return success/failure:
```csharp
return ToolResult.Success(jsonContent);  // content can be JSON or plain text
return ToolResult.Failure("Error message");
```

### System Prompt Design

- Keep static (no dynamic values) to enable OpenRouter prompt caching
- Include: agent role, tool descriptions, endpoint documentation, task guidelines
- Task services use `const string SystemPrompt` for this reason
- All dynamic data (suspects, locations, etc.) retrieved via tool calls

### Debugging

- Logs go to both `ILogger<AgentRunner>` and TaskLogService
- TaskLogService routes to UI in real-time via Blazor
- Check browser console for Blazor runtime errors
- Use `_logWriter?.Invoke()` in tools to add debug info to UI

### Model Configuration

The LLM model is configurable via `AgentOptions.Model` (e.g., "claude-3-5-sonnet-20241022" from OpenRouter). Override per-run with `RunAsync(..., modelOverride: "...")`.

## File Organization Notes

- **Agent.Core**: Pure business logic, no UI dependencies
- **Agent.UI**: Blazor presentation layer only
- **Configuration**: All external settings in `AgentOptions`
- **Tasks**: High-level orchestration; system prompts are const strings
- **Tools**: Low-level actions; always stateless or use DI for state
