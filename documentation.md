# Antimatter Documentation

Welcome to the **Antimatter** project! This document outlines our open-source philosophy, how to contribute, and the future vision for this suite of AI tools for Unity.

## Open Source Philosophy

Antimatter is built on the belief that AI should empower creators by automating the tedious parts of game development. We are an open-source project dedicated to providing high-quality, professional-grade tools that are:

*   **Transparency**: No "black boxes." All tool orchestration and dispatching logic is open for inspection.
*   **Extensibility**: Designed to be modular, so anyone can add new AI capabilities or providers.
*   **Community-Driven**: We value contributions from developers, technical artists, and AI researchers.

## Contributing

We welcome contributions of all kinds! Whether you're fixing a bug, adding a new feature, or improving documentation, here’s how you can help:

### 1. Adding New Agent Tools
If you want the AI agent to have a new capability (e.g., "Add a NavMesh" or "Export to GLB"), you can contribute a new entry to the `EditorToolDispatcher.cs` in the `Editor` assembly.

### 2. Improving Providers
We aim to support as many LLM providers as possible. If you’d like to see support for a new model or API, you can implement the `IGamePlanClient` interface in the `Runtime` assembly.

### 3. Reporting Issues
Found a bug or have a feature request? Please use our issue tracker (link to repository issues) to let us know.

## License

This project is licensed under the **MIT License**. This means you are free to use, modify, and distribute Antimatter in both personal and commercial projects. For the full legal text, please see the `LICENSE` file in the root directory.

## Roadmap

The future of Antimatter is bright! Some of the features we're currently working on include:

*   **Native Terminal Support**: Integrating a PTY-backed terminal directly into the Agent Window.
*   **Visual Diff System**: Enhancing the `PlanReviewWindow` to show 3D ghost previews of proposed scene changes.
*   **Local LLM Support**: Enabling the use of local models (e.g., via Llama.cpp or LocalAI) for offline workflows.
*   **Better Context Injection**: Improving the `SceneObserver` to provide more token-efficient representations of massive scenes.

---

### Community & Support

*   **Patreon**: [https://www.patreon.com/umbgames]
*   **Documentation Site**: [https://umbtechnologies.com]

Feel free to reach out with any questions or ideas. Let's build the future of agentic game development together!
