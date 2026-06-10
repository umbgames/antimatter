# Antimatter Runtime

This assembly contains the core logic and data structures for the Antimatter system that are safe to include in a built player.

## Core Components

### 1. Terrain Generation
*   **`TerrainGenerator`**: A static utility for build-time or runtime procedural heightmap generation.
*   **`TerrainAssetApplier`**: Handles the application of textures (Splatmaps), detail meshes (Grass), and tree/rock scattering based on a `TerrainAssetProfile`.
*   **`TerrainAssetProfile`**: A ScriptableObject that defines the rules for how a terrain should be populated.

### 2. AI & Schema
*   **`AgentMessage` & `AgentHistory`**: Data structures for maintaining conversational state with LLM providers.
*   **`GenerationSchema`**: The JSON-serializable format used to define "Game Plans" that the AI generates and the Editor executes.
*   **`IGamePlanClient`**: The interface for implementing new AI model providers.

### 3. Settings
*   **`AiProviderSettings`**: Static access to project-wide AI configuration (API keys, model selection, etc.). Uses `EditorPrefs` in the editor and safe defaults at runtime.

## Usage in Builds
While primarily an editor-first toolset, the `Runtime` assembly allows you to trigger procedural generation during gameplay (e.g., for roguelikes or infinite maps). Ensure any scripts referencing `UnityEditor` in this assembly are properly wrapped with `#if UNITY_EDITOR`.
