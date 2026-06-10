# Antimatter RoadGen

This submodule provides the tools and algorithms for the procedurally generated road networks, intersections, bridges, and tunnels used by the Antimatter AI.

## Core Systems

### 1. Planning & Routing
*   **`RoadNetworkPlanner`**: The high-level orchestrator that takes a list of hubs and arranges paths between them.
*   **`SettlementNetworkPlanner`**: Handles the layout of urban road grids and building plots within settlements.
*   **`AStarRouter`**: A specialized pathfinding implementation for generating road paths that avoid obstacles and adhere to grade/slope constraints.
*   **`TerrainCostGrid`**: Analyzes the terrain's slope and height to provide movement costs for the router.

### 2. Geometry Construction
*   **`BridgeBuilder`**: Generates procedural meshes for bridges when a road crosses a valley or water.
*   **`TunnelBuilder`**: Handles the generation of tunnel geometry and terrain hole cutting via `TerrainHoleCutter`.
*   **`IntersectionPatchBuilder`**: Procedurally creates mesh "patches" for smooth road junctions (T-junctions, cross-forks).
*   **`RoadCenterlineConnector`**: Joins individual road segments into continuous paths.

### 3. Utility
*   **`PolylineUtils`**: Geometric helpers for offsetting, smoothing, and intersecting polylines.
*   **`RoadGenSettings` & `SettlementGenSettings`**: Configuration profiles for road widths, bridge types, and city grid styles.

## Usage
Road networks are typically triggered as a "post-generation pass" by the `GameGenerationExecutor`. The AI agent provides hub positions in its `WorldGenSettings`, and this system builds the physical geometry across the terrain.
