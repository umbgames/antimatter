# Antimatter

Antimatter is an AI-powered game generation and scene arrangement toolset for Unity. It allows for agentic control over the editor, procedural terrain generation, and automated level layout using LLM-derived plans.

## Requirements

*   **Unity 2022.3+** (Standard or Universal Render Pipeline)
*   **Newtonsoft.Json (Unity NuGet version)**: This project depends on `com.unity.nuget.newtonsoft-json`. Ensure this is added to your `Packages/manifest.json`.

## Project Structure

Antimatter is divided into two main assembly modules to support both runtime and editor functionality:

### 1. Antimatter Runtime
Located in `Assets/Antimatter/Runtime`, this contains the logic that can be safely used during gameplay or in builds.
*   **Terrain Generation (`TerrainGenerator.cs`, `TerrainAssetApplier.cs`)**: Procedural heightmap, splatmap, and detail scattering logic.
*   **AI Schema & Messaging**: Definitions for agent communication and generation plans.
*   **Settings Provider**: Shared configuration for AI providers (OpenAI, Groq, Gemini).

### 2. Antimatter Editor
Located in `Assets/Antimatter/Editor`, this contains tools that run exclusively within the Unity Editor.
*   **Agent Window**: The primary interface for interacting with the AI Agent.
*   **Orchestrator & Dispatchers**: Systems that handle streaming and tool execution.
*   **Scene Observers**: Monitors scene changes to provide context to the agent.
*   **Layout Passes**: Specialized logic for city, race track, and FPS level arrangement.

## Getting Started

### 1. API Configuration
Open the **Antimatter Settings** window (Tools -> Antimatter -> Settings) to configure your preferred AI provider (OpenAI, Groq, or Gemini) and enter your API Key.

### 2. Terrain Generation
To generate terrain procedurally:
1.  Create a `TerrainAssetProfile` in your project folder.
2.  Open the **Terrain Generator** window (Tools -> Antimatter -> Terrain -> Generator).
3.  Assign your profile and click **Generate**.

### 3. Agentic Workflow
Open the **Agent Window** (Tools -> Antimatter -> Agent). You can give the agent high-level tasks like "Create a small fantasy village with a central square" or "Add a player character with a basic movement script". The agent will use its tools to inspect the scene, plan, and execute the changes for you.

---
*Created by Antimatter developers for professional-grade Unity workflows.*
