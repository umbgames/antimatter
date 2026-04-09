# Antimatter Editor

This assembly contains the user interface, orchestrator, and execution logic for Antimatter's AI-driven editor tools. It is excluded from player builds.

## Key Systems

### 1. Agent Orchestration
*   **`AgentOrchestrator`**: Manages the multi-turn "agent loop". It coordinates between the AI provider, the tool dispatcher, and the UI.
*   **`EditorToolDispatcher`**: The bridge between the AI's "tool calls" and actual Unity Editor API calls (e.g., creating objects, modifying transforms, writing scripts).
*   **`ProjectObserver` & `SceneObserver`**: Passive monitors that provide the AI with context about the current project structure and active scene hierarchy.

### 2. Game Generation Execution
*   **`GameGenerationExecutor`**: Parses `GameGeneration` plans (JSON) and translates them into sequence of editor actions (creating scenes, spawning objects, setting up components).
*   **`CityRootPlacer` & `LayoutPasses`**: Specialized procedural arrangement logic that cleans up AI-generated layouts by snapping objects to terrain, flattening ground, and organizing hierarchies.

### 3. Windows & UI
*   **`AgentWindow`**: The main chat interface for interacting with the AI.
*   **`PlanReviewWindow`**: A visual diff/review tool for inspecting a proposed "Game Plan" before it is executed.
*   **`TerrainGeneratorWindow`**: A dedicated UI for the procedural terrain system.

## Extending the Agent
To add a new capability to the AI agent:
1.  Define a new tool in `EditorToolDispatcher.cs` (add to the `_tools` list).
2.  Provide a JSON Schema for its parameters.
3.  Implement the logic using `Undo` registered editor calls.
4.  The agent will automatically see the new tool in its system prompt on the next request.
