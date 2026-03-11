# FindHim Task - Implementation Plan

## Overview

Implement the S01E02 "findhim" task using an **agentic function calling** approach with a hybrid tool architecture (4 tools), static system prompt for cache efficiency, and rich result type for iteration limit handling.

---

## Task Description

Find which suspect from S01E01 was near a nuclear power plant, determine their access level, identify the power plant code, and submit the answer to the Hub.

---

## Architecture Summary

- **Approach**: Agent with Function Calling (LLM drives the investigation)
- **Tools**: 4 hybrid tools (1 generic API tool + 3 specialized)
- **Iteration Limit**: 15 max LLM calls with rich result reporting
- **Prompt Caching**: Static system prompt, all dynamic data via tools

---

## Implementation Checklist

### Phase 1: Core Infrastructure

- [ ] **Create `Agent.Core/Agent/AgentRunResult.cs`**
  - Record type with: Success, Response, IterationsUsed, LimitReached, ToolCallsCount
  - No dependencies, implement first

- [ ] **Modify `Agent.Core/Agent/AgentRunner.cs`**
  - Change return type from `string` to `AgentRunResult`
  - Add `maxIterations` parameter (default: 15)
  - Add iteration counter in loop
  - Log warning at 80% of limit
  - Return rich result when limit reached

- [ ] **Create `Agent.Core/Utils/GeoCalculator.cs`**
  - Static class with `HaversineDistanceKm(lat1, lon1, lat2, lon2)` method
  - Earth radius constant: 6371 km
  - Pure math, no dependencies

---

### Phase 2: Tools

- [ ] **Create `Agent.Core/Tools/Implementations/LoadSuspectsTool.cs`**
  - Name: `load_suspects`
  - Reads from `files/suspects.json`
  - Returns array of { name, surname, birthYear }
  - No parameters

- [ ] **Create `Agent.Core/Tools/Implementations/HubApiQueryTool.cs`**
  - Name: `hub_api_query`
  - Parameters: endpoint (enum), name, surname, birthYear (optional based on endpoint)
  - Supported endpoints:
    - `power_plants` → GET `/data/{key}/findhim_locations.json`
    - `location` → POST `/api/location` (requires name, surname)
    - `accesslevel` → POST `/api/accesslevel` (requires name, surname, birthYear)
  - Injects API key from AgentOptions
  - Validates required params per endpoint

- [ ] **Create `Agent.Core/Tools/Implementations/CalculateDistanceTool.cs`**
  - Name: `calculate_distance`
  - Parameters: lat1, lon1, lat2, lon2
  - Uses GeoCalculator.HaversineDistanceKm
  - Returns { distanceKm: number }

- [ ] **Create `Agent.Core/Tools/Implementations/SubmitFindHimAnswerTool.cs`**
  - Name: `submit_findhim_answer`
  - Parameters: name, surname, accessLevel, powerPlant
  - POSTs to `/verify` with task "findhim"
  - Returns Hub response (flag on success)

---

### Phase 3: Task Service

- [ ] **Modify `Agent.Core/Tasks/People/PeopleTaskService.cs`**
  - Add `SaveSuspectsToFile(List<PersonResult> suspects, string path)` method
  - Saves to `files/suspects.json` with name, surname, birthYear
  - Call after TagAndFilterAsync completes

- [ ] **Create `Agent.Core/Tasks/FindHim/FindHimTaskService.cs`**
  - Static system prompt (const string) with:
    - Agent role (investigator)
    - Tool descriptions and usage guidelines
    - Task instructions (non-prescriptive)
    - Endpoint documentation for hub_api_query
  - RunAsync method that:
    - Uses AgentRunner with maxIterations = 15
    - Handles AgentRunResult (log if limit reached)
    - Forwards progress via TaskLogWriter

---

### Phase 4: DI & UI Integration

- [ ] **Modify `Agent.UI/Program.cs`**
  - Register LoadSuspectsTool in ToolRegistry
  - Register HubApiQueryTool in ToolRegistry
  - Register CalculateDistanceTool in ToolRegistry
  - Register SubmitFindHimAnswerTool in ToolRegistry
  - Register FindHimTaskService as scoped/singleton

- [ ] **Modify `Agent.UI/Components/Pages/TaskRunner.razor`**
  - Add to _tasks array: ("findhim", "Find Him Task", "Find suspect near nuclear power plant")
  - Add RunFindHimTaskAsync method
  - Wire up task selection

---

## File Structure

```
Agent.Core/
├── Agent/
│   ├── AgentRunner.cs              ← MODIFY
│   └── AgentRunResult.cs           ← NEW
├── Utils/                          ← NEW FOLDER
│   └── GeoCalculator.cs            ← NEW
├── Tasks/
│   ├── People/
│   │   └── PeopleTaskService.cs    ← MODIFY
│   └── FindHim/                    ← NEW FOLDER
│       └── FindHimTaskService.cs   ← NEW
└── Tools/
    └── Implementations/
        ├── LoadSuspectsTool.cs     ← NEW
        ├── HubApiQueryTool.cs      ← NEW
        ├── CalculateDistanceTool.cs ← NEW
        └── SubmitFindHimAnswerTool.cs ← NEW

Agent.UI/
├── Program.cs                      ← MODIFY
└── Components/Pages/
    └── TaskRunner.razor            ← MODIFY

files/
└── suspects.json                   ← GENERATED AT RUNTIME
```

---

## Tool Specifications

### load_suspects
| Property | Value |
|----------|-------|
| Parameters | None |
| Returns | `[{ name, surname, birthYear }, ...]` |

### hub_api_query
| Parameter | Type | Required For |
|-----------|------|--------------|
| endpoint | "power_plants" \| "location" \| "accesslevel" | All |
| name | string | location, accesslevel |
| surname | string | location, accesslevel |
| birthYear | integer | accesslevel |

### calculate_distance
| Parameter | Type |
|-----------|------|
| lat1 | number |
| lon1 | number |
| lat2 | number |
| lon2 | number |
| **Returns** | `{ distanceKm: number }` |

### submit_findhim_answer
| Parameter | Type |
|-----------|------|
| name | string |
| surname | string |
| accessLevel | integer |
| powerPlant | string (PWR####PL) |

---

## AgentRunResult Fields

| Field | Type | Description |
|-------|------|-------------|
| Success | bool | True if completed naturally |
| Response | string | Final response or last state |
| IterationsUsed | int | Number of LLM calls made |
| LimitReached | bool | True if max iterations hit |
| ToolCallsCount | int | Total tool invocations |

---

## System Prompt Guidelines

Keep static (no dynamic values) to enable prompt caching:
- Role and personality
- Tool descriptions
- Endpoint documentation
- Task guidelines (non-prescriptive)

All dynamic data (suspects, locations, etc.) retrieved via tool calls.

---

## Configuration

- API key: Existing `AgentOptions.AiDevsKey`
- Hub URL: Existing `AgentOptions.HubBaseUrl`
- Suspects file: Hardcoded `files/suspects.json`
- Max iterations: 15 (passed to AgentRunner)

---

## Testing Notes

1. Run PeopleTaskService first to generate `suspects.json`
2. FindHim task requires suspects file to exist
3. Watch iteration count - if consistently hitting limit, may need prompt tuning
