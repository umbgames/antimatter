// Assets/Editor/AgentOrchestrator.cs
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Antimatter
{
    /// <summary>
    /// Core agentic loop with aggressive token-budget management.
    /// Sends conversation history + tools to the LLM, dispatches tool calls,
    /// feeds results back, and repeats until the model replies with plain text
    /// or the step budget is exhausted.
    ///
    /// TOKEN OPTIMIZATION STRATEGY:
    /// 1. Sliding context window — only the N most-recent messages are sent.
    /// 2. Tool result compression — large outputs are truncated with a hash.
    /// 3. History summarization — old turns are collapsed into a single summary.
    /// 4. Smart tool filtering — only contextually-relevant tools are included.
    /// 5. Token estimation — pre-flight check prevents 413 errors.
    /// </summary>
    public class AgentOrchestrator
    {
        // ── Static singleton for cross-system access ──
        public static AgentOrchestrator ActiveOrchestrator { get; private set; }

        // ── Events ──
        public event Action<AgentMessage> OnMessage;
        public event Action<int, int> OnStepProgress;   // (current, max)
        public event Action<int, int> OnTokenUsage;      // (prompt, completion)
        public event Action OnDone;
        public event Action<Exception> OnError;

        // ── State ──
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public int TotalPromptTokens { get; private set; }
        public int TotalCompletionTokens { get; private set; }

        // ── Internals ──
        private List<AgentMessage> _history = new List<AgentMessage>();
        private CancellationTokenSource _cts;
        private bool _cancelRequested;

        // Script tracking: .txt files that need renaming to .cs after the loop
        private List<string> _trackedScripts = new List<string>();

        // Session memory: scripts/objects created during this session (survives across RunAsync calls)
        private HashSet<string> _createdScripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _createdObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Deferred attachments: scripts to attach after domain reload
        private List<(string objectId, string scriptName)> _pendingAttachments = new List<(string, string)>();

        // External results injected by other systems (e.g. compiler errors)
        private Queue<(string toolName, string content)> _externalResults = new Queue<(string, string)>();

        // ── Token Budget Constants ──
        // Rough char-to-token ratio: ~4 chars per token for English text
        private const float CharsPerToken = 3.5f;

        // Maximum tokens we aim for in a single request payload (messages + tools)
        // Leaves headroom below the model's actual limit for the response
        private const int DefaultMaxRequestTokens = 6000;

        // Max characters allowed in any single tool result before truncation
        private const int MaxToolResultChars = 1500;

        // Max messages kept in the sliding context window (excluding system)
        private const int MaxContextMessages = 16;

        // Threshold: if a tool result is above this, summarize it instead
        private const int ToolResultSummaryThreshold = 800;

        public AgentOrchestrator()
        {
            ActiveOrchestrator = this;
        }

        // ── History management ──

        public void LoadHistory(List<AgentMessage> messages)
        {
            _history = new List<AgentMessage>(messages ?? new List<AgentMessage>());
        }

        public void ClearHistory()
        {
            _history.Clear();
            TotalPromptTokens = 0;
            TotalCompletionTokens = 0;
        }

        public List<AgentMessage> GetHistory()
        {
            return new List<AgentMessage>(_history);
        }

        public void InjectSystemMessage(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            var msg = AgentMessage.System(content);
            _history.Add(msg);
        }

        // ── Script tracking (text-swap trick) ──

        public void TrackScript(string txtPath)
        {
            if (!string.IsNullOrEmpty(txtPath) && !_trackedScripts.Contains(txtPath))
                _trackedScripts.Add(txtPath);
        }

        public void QueueAttachment(string objectId, string scriptName)
        {
            _pendingAttachments.Add((objectId, scriptName));
        }

        // ── External results (compiler errors etc.) ──

        public void AddExternalResult(string toolName, string content)
        {
            _externalResults.Enqueue((toolName, content));
        }

        // ── Controls ──

        public void Cancel()
        {
            _cancelRequested = true;
            _cts?.Cancel();
            IsPaused = false;
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }

        // ══════════════════════════════════════════════════════════════════
        //  TOKEN OPTIMIZATION ENGINE
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Estimates the token count for a string using a simple heuristic.
        /// ~3.5 characters per token for mixed English/code content.
        /// </summary>
        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (int)Math.Ceiling(text.Length / CharsPerToken);
        }

        /// <summary>
        /// Estimates total token count for a list of messages.
        /// Includes overhead per message (~4 tokens for role/delimiters).
        /// </summary>
        private static int EstimateHistoryTokens(List<AgentMessage> messages)
        {
            int total = 0;
            foreach (var msg in messages)
            {
                total += 4; // per-message overhead (role, separators)
                total += EstimateTokens(msg.content);
                total += EstimateTokens(msg.toolArgs);
                total += EstimateTokens(msg.toolName);
            }
            return total;
        }

        /// <summary>
        /// Estimates total tokens consumed by tool definitions.
        /// Each tool contributes: name + description + parameter schema.
        /// </summary>
        private static int EstimateToolDefinitionTokens(List<AgentTool> tools)
        {
            if (tools == null || tools.Count == 0) return 0;
            int total = 0;
            foreach (var t in tools)
            {
                total += 8; // per-tool overhead
                total += EstimateTokens(t.Name);
                total += EstimateTokens(t.Description);
                total += EstimateTokens(t.ParameterSchema);
            }
            return total;
        }

        /// <summary>
        /// Truncates a tool result string to fit within budget.
        /// Keeps the beginning and end, inserting a truncation marker.
        /// </summary>
        private static string CompressToolResult(string content, int maxChars)
        {
            if (string.IsNullOrEmpty(content) || content.Length <= maxChars)
                return content;

            int keepStart = (int)(maxChars * 0.65f);
            int keepEnd = maxChars - keepStart - 40; // 40 chars for the marker

            string start = content.Substring(0, keepStart);
            string end = content.Substring(content.Length - keepEnd);
            int truncatedCount = content.Length - keepStart - keepEnd;

            return $"{start}\n\n[...truncated {truncatedCount} chars...]\n\n{end}";
        }

        /// <summary>
        /// Builds the optimized context window to send to the LLM.
        /// Strategy:
        ///   1. Always include the system message (first).
        ///   2. Always include the most recent user message.
        ///   3. Keep the last N messages within the token budget.
        ///   4. Compress tool results that are too large.
        ///   5. If still over budget, summarize older messages.
        /// </summary>
        private List<AgentMessage> BuildOptimizedContext(List<AgentMessage> fullHistory, int tokenBudget)
        {
            if (fullHistory.Count == 0) return new List<AgentMessage>();

            var optimized = new List<AgentMessage>();

            // Step 1: Extract and always include the system message
            AgentMessage systemMsg = null;
            int historyStart = 0;
            if (fullHistory[0].role == AgentRole.System)
            {
                systemMsg = fullHistory[0];
                historyStart = 1;
            }

            // Step 2: Get non-system messages
            var recentMessages = new List<AgentMessage>();
            for (int i = historyStart; i < fullHistory.Count; i++)
                recentMessages.Add(fullHistory[i]);

            // Step 3: Compress all tool results in the recent messages
            for (int i = 0; i < recentMessages.Count; i++)
            {
                var msg = recentMessages[i];
                if (msg.role == AgentRole.Tool && !string.IsNullOrEmpty(msg.content))
                {
                    if (msg.content.Length > MaxToolResultChars)
                    {
                        // Create a compressed copy (don't mutate original)
                        recentMessages[i] = new AgentMessage
                        {
                            role = msg.role,
                            content = CompressToolResult(msg.content, MaxToolResultChars),
                            toolCallId = msg.toolCallId,
                            toolName = msg.toolName,
                            timestampTicks = msg.timestampTicks
                        };
                    }
                }
            }

            // Step 4: Apply sliding window — keep last MaxContextMessages
            if (recentMessages.Count > MaxContextMessages)
            {
                // Summarize the messages we're dropping
                int dropCount = recentMessages.Count - MaxContextMessages;
                var droppedMessages = recentMessages.GetRange(0, dropCount);
                string summary = SummarizeDroppedMessages(droppedMessages);

                recentMessages = recentMessages.GetRange(dropCount, MaxContextMessages);

                // Insert summary as a system-level context reminder
                if (!string.IsNullOrEmpty(summary))
                {
                    recentMessages.Insert(0, AgentMessage.System(summary));
                }
            }

            // Step 5: Build final list and check token budget
            if (systemMsg != null) optimized.Add(systemMsg);
            optimized.AddRange(recentMessages);

            // Step 6: If still over budget, progressively trim
            int estimatedTokens = EstimateHistoryTokens(optimized);
            while (estimatedTokens > tokenBudget && optimized.Count > 3)
            {
                // Remove the oldest non-system message
                int removeIdx = (optimized[0].role == AgentRole.System) ? 1 : 0;
                // Don't remove if it's a system context summary we just added
                if (removeIdx < optimized.Count && optimized[removeIdx].role == AgentRole.System)
                    removeIdx++;
                if (removeIdx >= optimized.Count - 1) break; // Don't remove the last message

                optimized.RemoveAt(removeIdx);
                estimatedTokens = EstimateHistoryTokens(optimized);
            }

            // Step 7: Final aggressive truncation of tool results if still over
            if (estimatedTokens > tokenBudget)
            {
                for (int i = 0; i < optimized.Count && estimatedTokens > tokenBudget; i++)
                {
                    if (optimized[i].role == AgentRole.Tool && optimized[i].content?.Length > 300)
                    {
                        optimized[i] = new AgentMessage
                        {
                            role = optimized[i].role,
                            content = CompressToolResult(optimized[i].content, 300),
                            toolCallId = optimized[i].toolCallId,
                            toolName = optimized[i].toolName,
                            timestampTicks = optimized[i].timestampTicks
                        };
                        estimatedTokens = EstimateHistoryTokens(optimized);
                    }
                }
            }

            return optimized;
        }

        /// <summary>
        /// Creates a brief summary of older messages that are being dropped
        /// from the context window. This preserves key context without the
        /// full token cost.
        /// </summary>
        private string SummarizeDroppedMessages(List<AgentMessage> dropped)
        {
            if (dropped == null || dropped.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine("[CONTEXT SUMMARY — Earlier conversation turns]");

            // Extract just the key actions and results
            int toolCallCount = 0;
            var toolNames = new HashSet<string>();
            var keyPoints = new List<string>();

            foreach (var msg in dropped)
            {
                switch (msg.role)
                {
                    case AgentRole.User:
                        // Keep user requests as-is (they're usually short)
                        string userContent = msg.content?.Length > 200
                            ? msg.content.Substring(0, 200) + "..."
                            : msg.content;
                        keyPoints.Add($"User asked: {userContent}");
                        break;

                    case AgentRole.Assistant when msg.IsToolCall:
                        toolCallCount++;
                        if (!string.IsNullOrEmpty(msg.toolName))
                            toolNames.Add(msg.toolName);
                        break;

                    case AgentRole.Assistant:
                        if (!string.IsNullOrEmpty(msg.content))
                        {
                            string assistContent = msg.content.Length > 150
                                ? msg.content.Substring(0, 150) + "..."
                                : msg.content;
                            keyPoints.Add($"Agent said: {assistContent}");
                        }
                        break;

                    case AgentRole.Tool:
                        // Just note success/failure, not the full result
                        bool isError = msg.content?.StartsWith("ERROR:") == true;
                        string status = isError ? "FAILED" : "OK";
                        keyPoints.Add($"Tool {msg.toolName}: {status}");
                        break;
                }
            }

            if (toolCallCount > 0)
                sb.AppendLine($"Agent executed {toolCallCount} tool calls: {string.Join(", ", toolNames)}");

            foreach (var point in keyPoints.Take(8)) // Max 8 key points from older context
                sb.AppendLine($"  • {point}");

            if (keyPoints.Count > 8)
                sb.AppendLine($"  (and {keyPoints.Count - 8} more actions...)");

            return sb.ToString();
        }

        /// <summary>
        /// Filters tools to include only those relevant to the current context.
        /// For models with very small token budgets, this significantly reduces overhead.
        /// </summary>
        private List<AgentTool> FilterToolsForContext(List<AgentTool> allTools, List<AgentMessage> history)
        {
            if (allTools == null || allTools.Count == 0) return allTools;

            // Always include these core tools
            var coreTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "inspectScene", "createObject", "deleteObject", "modifyTransform",
                "writeScript", "attachScript", "createMaterial", "readConsoleErrors",
                "saveScene", "addComponent", "setObjectActive", "focusSceneView"
            };

            // Check if history contains hints about what tools might be needed
            string historyText = string.Join(" ", history
                .Where(m => !string.IsNullOrEmpty(m.content))
                .Select(m => m.content)
                .TakeLast(5)); // Only scan last 5 messages

            bool needsTerrain = historyText.IndexOf("terrain", StringComparison.OrdinalIgnoreCase) >= 0;
            bool needsRoads = historyText.IndexOf("road", StringComparison.OrdinalIgnoreCase) >= 0;
            bool needsWorldGen = historyText.IndexOf("city", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  historyText.IndexOf("town", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  historyText.IndexOf("settlement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  historyText.IndexOf("village", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  historyText.IndexOf("medieval", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  historyText.IndexOf("modern", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  historyText.IndexOf("suburban", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  historyText.IndexOf("world", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  historyText.IndexOf("building", StringComparison.OrdinalIgnoreCase) >= 0;
            bool needsUI = historyText.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            historyText.IndexOf("canvas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            historyText.IndexOf("button", StringComparison.OrdinalIgnoreCase) >= 0;
            bool needsAssetSearch = historyText.IndexOf("search", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     historyText.IndexOf("find", StringComparison.OrdinalIgnoreCase) >= 0;
            bool needsPlan = historyText.IndexOf("plan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              historyText.IndexOf("game", StringComparison.OrdinalIgnoreCase) >= 0;
            bool needsPlayMode = historyText.IndexOf("play", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  historyText.IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0;

            // Add contextual tools
            if (needsTerrain) coreTools.Add("generateTerrain");
            if (needsRoads) { coreTools.Add("generateRoadNetwork"); coreTools.Add("generateWorld"); }
            if (needsWorldGen)
            {
                coreTools.Add("generateSettlements"); coreTools.Add("generateCity");
                coreTools.Add("arrangeCityScene"); coreTools.Add("generateWorld");
                coreTools.Add("generateTerrain"); coreTools.Add("generateRoadNetwork");
            }
            if (needsUI) coreTools.Add("generateCanvasUI");
            if (needsAssetSearch) { coreTools.Add("searchAssets"); coreTools.Add("listAssets"); }
            if (needsPlan) coreTools.Add("runFullPlan");
            if (needsPlayMode) { coreTools.Add("enterPlayMode"); coreTools.Add("exitPlayMode"); }

            // Also add listAssets always since it's small and useful
            coreTools.Add("listAssets");
            // Add createScene since it's commonly needed
            coreTools.Add("createScene");

            // Filter
            var filtered = allTools.Where(t => coreTools.Contains(t.Name)).ToList();

            // Also include any marketplace tools that were dynamically registered (they're usually important)
            foreach (var t in allTools)
            {
                if (!filtered.Contains(t) && !IsBuiltinTool(t.Name))
                    filtered.Add(t);
            }

            return filtered;
        }

        private static bool IsBuiltinTool(string name)
        {
            return name switch
            {
                "inspectScene" or "listAssets" or "searchAssets" or "createObject" or
                "deleteObject" or "modifyTransform" or "addComponent" or "writeScript" or
                "deleteScript" or "createMaterial" or "readConsoleErrors" or "saveScene" or
                "createScene" or "runFullPlan" or "attachScript" or "generateTerrain" or
                "setObjectActive" or "generateRoadNetwork" or "generateSettlements" or
                "generateCity" or "generateWorld" or "arrangeCityScene" or "focusSceneView" or
                "generateCanvasUI" or "createParticleSystem" or "enterPlayMode" or "exitPlayMode" => true,
                _ => false
            };
        }

        /// <summary>
        /// Calculates the effective token budget for messages given the tool overhead.
        /// </summary>
        private int GetMessageTokenBudget(List<AgentTool> tools)
        {
            int toolTokens = EstimateToolDefinitionTokens(tools);
            int budget = DefaultMaxRequestTokens - toolTokens - 200; // 200 token buffer for response overhead
            return Math.Max(budget, 1500); // Never go below 1500 tokens for messages
        }

        // ══════════════════════════════════════════════════════════════════
        //  MAIN AGENT LOOP
        // ══════════════════════════════════════════════════════════════════

        public async Task RunAsync(string goal)
        {
            if (IsRunning) return;

            IsRunning = true;
            _cancelRequested = false;
            _trackedScripts.Clear();
            _pendingAttachments.Clear();
            ActiveOrchestrator = this;

            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(AiProviderSettings.HttpTimeoutSeconds * 10));

            try
            {
                // Inject system prompt if history is empty or doesn't start with one
                if (_history.Count == 0 || _history[0].role != AgentRole.System)
                {
                    var sysMsg = AgentMessage.System(AgentSystemPrompt.Build());
                    _history.Insert(0, sysMsg);
                }

                // ── SMART CONTINUATION: If user says "continue", inject context about existing work ──
                if (IsContinuationRequest(goal))
                {
                    string existingWorkContext = BuildExistingWorkContext();
                    if (!string.IsNullOrEmpty(existingWorkContext))
                    {
                        _history.Add(AgentMessage.System(existingWorkContext));
                    }
                }

                // Add user message
                var userMsg = AgentMessage.User(goal);
                _history.Add(userMsg);
                OnMessage?.Invoke(userMsg);

                int maxSteps = AiProviderSettings.AgentMaxSteps; // 0 = unlimited
                bool unlimited = maxSteps <= 0;
                var allTools = EditorToolDispatcher.GetAllTools();
                IGamePlanClient client = AiProviderSettings.CreateClient();
                int consecutiveTextReplies = 0; // Track text-only replies for auto-continue
                string stopReason = null;

                for (int step = 1; unlimited || step <= maxSteps; step++)
                {
                    if (_cancelRequested) { stopReason = "Cancelled by user."; break; }

                    // Wait while paused
                    while (IsPaused && !_cancelRequested)
                        await Task.Delay(200);
                    if (_cancelRequested) { stopReason = "Cancelled by user."; break; }

                    OnStepProgress?.Invoke(step, unlimited ? 0 : maxSteps);

                    // Check for external results injected by other systems
                    while (_externalResults.Count > 0)
                    {
                        var ext = _externalResults.Dequeue();
                        var extMsg = AgentMessage.ToolResult("external", ext.toolName, ext.content);
                        _history.Add(extMsg);
                        OnMessage?.Invoke(extMsg);
                    }

                    // ── TOKEN OPTIMIZATION: Build optimized payload ──
                    var filteredTools = FilterToolsForContext(allTools, _history);
                    int messageBudget = GetMessageTokenBudget(filteredTools);
                    var optimizedHistory = BuildOptimizedContext(_history, messageBudget);

                    int estimatedTotal = EstimateHistoryTokens(optimizedHistory) + EstimateToolDefinitionTokens(filteredTools);
                    Debug.Log($"[Agent] Step {step}: Sending ~{estimatedTotal} est. tokens " +
                              $"({optimizedHistory.Count} msgs, {filteredTools.Count}/{allTools.Count} tools)");

                    // Call the LLM with optimized payload
                    AgentResponse response;
                    try
                    {
                        response = await client.GenerateAgentResponseAsync(optimizedHistory, filteredTools, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        stopReason = "Request timed out or was cancelled.";
                        break;
                    }
                    catch (Exception ex)
                    {
                        // If we hit a token limit error, retry with more aggressive compression
                        if (IsTokenLimitError(ex))
                        {
                            Debug.LogWarning($"[Agent] Token limit hit. Retrying with aggressive compression...");
                            var aggressiveHistory = BuildOptimizedContext(_history, messageBudget / 2);
                            try
                            {
                                response = await client.GenerateAgentResponseAsync(aggressiveHistory, filteredTools, _cts.Token);
                            }
                            catch (Exception retryEx)
                            {
                                OnError?.Invoke(retryEx);
                                stopReason = $"Token limit retry failed: {retryEx.Message}";
                                break;
                            }
                        }
                        else
                        {
                            OnError?.Invoke(ex);
                            stopReason = $"LLM request error: {ex.Message}";
                            break;
                        }
                    }

                    // Track token usage
                    TotalPromptTokens += response.PromptTokens;
                    TotalCompletionTokens += response.CompletionTokens;
                    OnTokenUsage?.Invoke(response.PromptTokens, response.CompletionTokens);

                    // If model replied with text only (no tool calls)
                    if (!response.HasToolCalls)
                    {
                        if (response.HasText)
                        {
                            var assistantMsg = AgentMessage.AssistantText(response.TextContent);
                            _history.Add(assistantMsg);
                            OnMessage?.Invoke(assistantMsg);

                            // ── AUTO-CONTINUE: Check if the model is mid-task and just paused ──
                            if (ShouldAutoContinue(response.TextContent) && consecutiveTextReplies < 2)
                            {
                                consecutiveTextReplies++;
                                Debug.Log($"[Agent] Auto-continuing: model appears mid-task (attempt {consecutiveTextReplies})");
                                continue; // Don't break — let the loop send the history back
                            }
                            if (consecutiveTextReplies >= 2)
                                stopReason = "Model sent text-only replies 2 times in a row (auto-continue limit).";
                            else
                                stopReason = "Model replied with text and no tool calls (appears finished).";
                        }
                        else
                        {
                            stopReason = "Model returned an empty response with no tool calls.";
                        }
                        break;
                    }
                    consecutiveTextReplies = 0; // Reset on tool calls

                    // Process tool calls
                    foreach (var call in response.ToolCalls)
                    {
                        if (_cancelRequested) { stopReason = "Cancelled by user during tool execution."; break; }

                        // Add the tool call to history
                        var callMsg = AgentMessage.AssistantToolCall(call.CallId, call.ToolName, call.Arguments?.ToString() ?? "{}");
                        _history.Add(callMsg);
                        OnMessage?.Invoke(callMsg);

                        // Confirmation mode
                        if (AiProviderSettings.AgentConfirmMode)
                        {
                            bool confirmed = EditorUtility.DisplayDialog(
                                "Confirm Tool Call",
                                $"The agent wants to execute:\n\n{EditorToolDispatcher.GetFriendlyToolName(call.ToolName)}\n\nAllow?",
                                "Yes", "No");
                            if (!confirmed)
                            {
                                var skipResult = AgentMessage.ToolResult(call.CallId, call.ToolName, "Tool call was rejected by user.");
                                _history.Add(skipResult);
                                OnMessage?.Invoke(skipResult);
                                continue;
                            }
                        }

                        // Execute the tool
                        AgentToolResult result;
                        try
                        {
                            result = EditorToolDispatcher.Dispatch(call.ToolName, call.Arguments);

                            // ── SESSION MEMORY: Track what was created ──
                            TrackCreatedArtifact(call.ToolName, call.Arguments);
                        }
                        catch (Exception ex)
                        {
                            result = AgentToolResult.Fail($"Tool execution error: {ex.Message}");
                        }

                        // ── TOKEN OPTIMIZATION: Compress tool result before storing ──
                        string resultContent = result.ToString();
                        if (resultContent.Length > MaxToolResultChars)
                        {
                            Debug.Log($"[Agent] Compressing tool result from {resultContent.Length} to ~{MaxToolResultChars} chars");
                            resultContent = CompressToolResult(resultContent, MaxToolResultChars);
                        }

                        // Add tool result to history
                        var resultMsg = AgentMessage.ToolResult(call.CallId, call.ToolName, resultContent);
                        _history.Add(resultMsg);
                        OnMessage?.Invoke(resultMsg);
                    }

                    // If the response also had text content alongside tool calls, add it
                    if (response.HasText && response.HasToolCalls)
                    {
                        var textMsg = AgentMessage.AssistantText(response.TextContent);
                        _history.Add(textMsg);
                        OnMessage?.Invoke(textMsg);
                    }
                }

                // If the for-loop finished without breaking, we hit max steps (only possible if not unlimited)
                if (stopReason == null)
                    stopReason = $"Reached maximum step limit ({maxSteps}). Type 'continue' to resume, or set Max Agent Steps to 0 for unlimited.";

                // ── LOG STOP REASON ──
                Debug.Log($"[Agent] Stopped: {stopReason}");
                var stopMsg = AgentMessage.AssistantText($"⚠️ Agent stopped — {stopReason}");
                _history.Add(stopMsg);
                OnMessage?.Invoke(stopMsg);

                // Finalize: rename tracked scripts from .txt to .cs
                FinalizeScripts();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
                OnDone?.Invoke();
            }
        }

        /// <summary>
        /// Detects if an exception is caused by exceeding the model's token limit.
        /// </summary>
        private static bool IsTokenLimitError(Exception ex)
        {
            string msg = ex.Message ?? "";
            return msg.Contains("413") ||
                   msg.Contains("token") ||
                   msg.Contains("too large") ||
                   msg.Contains("Request too large") ||
                   msg.Contains("context_length_exceeded") ||
                   msg.Contains("rate_limit_exceeded");
        }

        // ══════════════════════════════════════════════════════════════════
        //  SMART CONTINUATION & SESSION MEMORY
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Detects if the user's message is a continuation request like "continue", "keep going", etc.
        /// </summary>
        private static bool IsContinuationRequest(string goal)
        {
            if (string.IsNullOrWhiteSpace(goal)) return false;
            string lower = goal.Trim().ToLowerInvariant();
            // Match common continuation phrases
            return lower == "continue" || lower == "go on" || lower == "keep going" ||
                   lower == "go ahead" || lower == "proceed" || lower == "finish" ||
                   lower == "finish it" || lower == "complete it" || lower == "carry on" ||
                   lower.StartsWith("continue ") || lower.StartsWith("keep ") ||
                   lower.StartsWith("finish ") || lower.StartsWith("go on ") ||
                   lower.StartsWith("carry on") || lower.StartsWith("proceed ");
        }

        /// <summary>
        /// Detects if the model's text reply looks like it wants to keep working
        /// rather than being a true final answer. This prevents the loop from
        /// breaking mid-generation.
        /// </summary>
        private static bool ShouldAutoContinue(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string lower = text.ToLowerInvariant();

            // If the model said "DONE" or gave a clear completion signal, stop
            if (lower.Contains("done") && (lower.Contains("summary") || lower.Contains("created") || lower.Contains("finished")))
                return false;

            // If it's a very short reply that looks like a pause/thinking message
            bool looksIncomplete =
                lower.Contains("let me ") ||
                lower.Contains("i'll ") || lower.Contains("i will ") ||
                lower.Contains("next ") || lower.Contains("now i") ||
                lower.Contains("continuing") || lower.Contains("moving on") ||
                lower.Contains("let's ") || lower.Contains("first, ") ||
                lower.Contains("step ") ||
                lower.Contains("i need to ") || lower.Contains("i should ") ||
                (text.TrimEnd().EndsWith(":") && text.Length < 200); // ends with colon = about to list actions

            return looksIncomplete;
        }

        /// <summary>
        /// Tracks scripts and objects created during tool execution so we can
        /// remind the AI what already exists on continuation.
        /// </summary>
        private void TrackCreatedArtifact(string toolName, Newtonsoft.Json.Linq.JObject args)
        {
            if (args == null) return;
            switch (toolName?.ToLowerInvariant())
            {
                case "writescript":
                    string path = (string)args["path"];
                    if (!string.IsNullOrEmpty(path))
                        _createdScripts.Add(path);
                    break;
                case "createobject":
                    string id = (string)args["id"];
                    if (!string.IsNullOrEmpty(id))
                        _createdObjects.Add(id);
                    break;
            }
        }

        /// <summary>
        /// Builds a context message about existing work so the AI doesn't re-create things.
        /// Scans both session memory and the actual project filesystem.
        /// </summary>
        private string BuildExistingWorkContext()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[IMPORTANT — EXISTING WORK CONTEXT]");
            sb.AppendLine("The following scripts and objects ALREADY EXIST. Do NOT re-create them.");
            sb.AppendLine("Pick up where you left off and complete any remaining work.\n");

            // From session memory
            if (_createdScripts.Count > 0)
            {
                sb.AppendLine("Scripts already written this session:");
                foreach (var s in _createdScripts)
                    sb.AppendLine($"  ✓ {s}");
            }
            if (_createdObjects.Count > 0)
            {
                sb.AppendLine("Objects already created this session:");
                foreach (var o in _createdObjects)
                    sb.AppendLine($"  ✓ {o}");
            }

            // Scan the project for Generated scripts on disk
            try
            {
                string genDir = "Assets/Generated";
                if (Directory.Exists(genDir))
                {
                    var existingScripts = Directory.GetFiles(genDir, "*.cs", SearchOption.AllDirectories);
                    if (existingScripts.Length > 0)
                    {
                        sb.AppendLine("\nScripts on disk in Assets/Generated/:");
                        foreach (var f in existingScripts.Take(20))
                            sb.AppendLine($"  ✓ {f.Replace('\\', '/')}");
                        if (existingScripts.Length > 20)
                            sb.AppendLine($"  ... and {existingScripts.Length - 20} more");
                    }
                }
            }
            catch { /* ignore filesystem errors */ }

            // Scan history for recent tool results to extract what was done
            int toolCallsDone = 0;
            var recentTools = new List<string>();
            for (int i = _history.Count - 1; i >= 0 && toolCallsDone < 15; i--)
            {
                var msg = _history[i];
                if (msg.role == AgentRole.Assistant && msg.IsToolCall)
                {
                    string desc = $"{msg.toolName}";
                    if (!string.IsNullOrEmpty(msg.toolArgs) && msg.toolArgs.Length < 100)
                        desc += $"({msg.toolArgs})";
                    recentTools.Add(desc);
                    toolCallsDone++;
                }
            }
            if (recentTools.Count > 0)
            {
                recentTools.Reverse();
                sb.AppendLine("\nRecent tool calls already executed:");
                foreach (var t in recentTools)
                    sb.AppendLine($"  ✓ {t}");
            }

            sb.AppendLine("\nDo NOT repeat any of the above. Continue from where you stopped.");
            return sb.ToString();
        }

        // ── JSON Fallback Mode (monolithic plan generation) ──

        public async Task RunJsonFallbackAsync(string goal, GenerationConstraints constraints, bool editCurrentScene, bool levelGen)
        {
            if (IsRunning) return;

            IsRunning = true;
            _cancelRequested = false;
            ActiveOrchestrator = this;

            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(AiProviderSettings.HttpTimeoutSeconds));

            try
            {
                var userMsg = AgentMessage.User(goal);
                _history.Add(userMsg);
                OnMessage?.Invoke(userMsg);

                OnStepProgress?.Invoke(1, 1);

                IGamePlanClient client = AiProviderSettings.CreateClient();

                // Build prompt for monolithic JSON plan
                string prompt = BuildJsonFallbackPrompt(goal, constraints, editCurrentScene, levelGen);

                string planJson = await client.GeneratePlanAsync(prompt, _cts.Token);

                if (string.IsNullOrWhiteSpace(planJson))
                    throw new Exception("Model returned empty plan.");

                var resultMsg = AgentMessage.AssistantText("Executing generated plan...");
                _history.Add(resultMsg);
                OnMessage?.Invoke(resultMsg);

                // Execute the plan
                if (editCurrentScene)
                    GameGenerationExecutor.ExecuteEdit(planJson);
                else if (levelGen)
                    GameGenerationExecutor.ExecuteCreate(planJson);
                else
                    GameGenerationExecutor.ExecuteCreate(planJson);

                var doneMsg = AgentMessage.AssistantText("Plan executed successfully.");
                _history.Add(doneMsg);
                OnMessage?.Invoke(doneMsg);
            }
            catch (OperationCanceledException)
            {
                var cancelMsg = AgentMessage.AssistantText("Generation was cancelled.");
                _history.Add(cancelMsg);
                OnMessage?.Invoke(cancelMsg);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
                OnDone?.Invoke();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════════════

        private void FinalizeScripts()
        {
            if (_trackedScripts.Count == 0 && _pendingAttachments.Count == 0) return;

            // Rename .txt → .cs
            foreach (string txtPath in _trackedScripts)
            {
                if (!File.Exists(txtPath)) continue;

                string csPath = txtPath;
                if (csPath.EndsWith(".txt"))
                    csPath = csPath.Substring(0, csPath.Length - 4); // removes .txt, leaving the .cs

                try
                {
                    if (File.Exists(csPath))
                        File.Delete(csPath);
                    File.Move(txtPath, csPath);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AgentOrchestrator] Failed to finalize script {txtPath}: {ex.Message}");
                }
            }

            _trackedScripts.Clear();

            // Trigger asset refresh so scripts compile
            AssetDatabase.Refresh();

            // Queue deferred script attachments
            if (_pendingAttachments.Count > 0)
            {
                var attachments = new List<(string, string)>(_pendingAttachments);
                _pendingAttachments.Clear();

                EditorApplication.delayCall += () =>
                {
                    // Wait for compilation to finish before attaching
                    void TryAttach()
                    {
                        if (EditorApplication.isCompiling)
                        {
                            EditorApplication.delayCall += TryAttach;
                            return;
                        }

                        MonoScriptResolver.RebuildIndex();
                        foreach (var (objectId, scriptName) in attachments)
                        {
                            try
                            {
                                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                                var go = FindInScene(scene, objectId);
                                if (go == null)
                                {
                                    Debug.LogWarning($"[AgentOrchestrator] Deferred attach: Object '{objectId}' not found.");
                                    continue;
                                }

                                var ms = MonoScriptResolver.Find(scriptName);
                                if (ms == null)
                                {
                                    Debug.LogWarning($"[AgentOrchestrator] Deferred attach: Script '{scriptName}' not found.");
                                    continue;
                                }

                                var type = ms.GetClass();
                                if (type != null)
                                {
                                    Undo.AddComponent(go, type);
                                    Debug.Log($"[AgentOrchestrator] Attached '{scriptName}' to '{objectId}'.");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[AgentOrchestrator] Deferred attach failed: {ex.Message}");
                            }
                        }
                    }

                    TryAttach();
                };
            }
        }

        private static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
        {
            if (!scene.IsValid()) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                var found = root.transform.Find(name);
                if (found != null) return found.gameObject;

                // Deep search
                var all = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in all)
                {
                    if (t.gameObject.name == name)
                        return t.gameObject;
                }
            }
            return null;
        }

        private string BuildJsonFallbackPrompt(string goal, GenerationConstraints constraints, bool editCurrentScene, bool levelGen)
        {
            var sb = new StringBuilder();

            if (editCurrentScene)
            {
                sb.AppendLine("You are a Unity scene editor. Modify the CURRENT scene based on the user's request.");
                sb.AppendLine("Return a valid JSON plan for GameGenerationExecutor.ExecuteEdit.");
            }
            else
            {
                sb.AppendLine("You are a Unity game generator. Create a complete game plan from scratch.");
                sb.AppendLine("Return a valid JSON plan for GameGenerationExecutor.ExecuteCreate.");
            }

            if (levelGen)
            {
                sb.AppendLine("Focus on level generation: create a compact, playable FPS-style level.");
            }

            sb.AppendLine();
            sb.AppendLine("CONSTRAINTS:");
            sb.AppendLine($"  maxObjectsPerScene: {constraints.maxObjectsPerScene}");
            sb.AppendLine($"  maxScenes: {constraints.maxScenes}");
            if (constraints.forbidScripts) sb.AppendLine("  NO SCRIPTS allowed.");
            if (constraints.forbidTerrain) sb.AppendLine("  NO TERRAIN allowed.");
            if (constraints.forbidMaterials) sb.AppendLine("  NO MATERIALS allowed.");
            if (constraints.enableAutoArrange) sb.AppendLine("  Auto-arrange enabled (roads/settlements).");

            sb.AppendLine();
            sb.AppendLine("USER REQUEST:");
            sb.AppendLine(goal);
            sb.AppendLine();
            sb.AppendLine("Return ONLY pure JSON. No markdown, no commentary.");

            return sb.ToString();
        }
    }
}