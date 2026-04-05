// Assets/Editor/AgentOrchestrator.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;

/// <summary>
/// The brain of the agentic loop. Manages conversation, sends to AI,
/// handles tool calls, feeds results back, and loops until done.
/// </summary>
public class AgentOrchestrator
{
    [Serializable]
    public class PendingAttachment
    {
        public string objectId;
        public string scriptName;
    }

    [Serializable]
    private class PendingAttachmentWrapper
    {
        public List<PendingAttachment> list = new List<PendingAttachment>();
    }

    // Static reference for external systems (like AutoFix) to talk to the active agent
    public static AgentOrchestrator ActiveOrchestrator { get; private set; }

    // Events
    public event Action<AgentMessage> OnMessage;
    public event Action<string, string> OnToolCallStarted;    // (toolName, argsJson)
    public event Action<string, AgentToolResult> OnToolCallCompleted; // (toolName, result)
    public event Action OnDone;
    public event Action<Exception> OnError;
    public event Action<int, int> OnStepProgress;  // (currentStep, maxSteps)
    public event Action<int, int> OnTokenUsage;    // (promptTokens, completionTokens)

    // State
    private List<AgentMessage> _history = new List<AgentMessage>();
    private int _totalPromptTokens;
    private int _totalCompletionTokens;
    private bool _isRunning;
    private bool _isPaused;
    private CancellationTokenSource _cts;

    public bool IsRunning => _isRunning;
    public bool IsPaused => _isPaused;
    public int TotalPromptTokens => _totalPromptTokens;
    public int TotalCompletionTokens => _totalCompletionTokens;
    public IReadOnlyList<AgentMessage> History => _history;

    private List<string> _tempWrittenScripts = new List<string>();
    private List<PendingAttachment> _queuedAttachments = new List<PendingAttachment>();

    public void TrackScript(string txtPath) => _tempWrittenScripts.Add(txtPath);
    public void QueueAttachment(string objectId, string scriptName) => _queuedAttachments.Add(new PendingAttachment { objectId = objectId, scriptName = scriptName });

    /// <summary>
    /// Main entry point: User sends a goal, agent loops until done.
    /// </summary>
    public async Task RunAsync(string userGoal)
    {
        if (_isRunning)
            throw new InvalidOperationException("Agent is already running.");

        ActiveOrchestrator = this;
        _isRunning = true;
        _isPaused = false;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            // Build system prompt
            string systemPrompt = AgentSystemPrompt.Build();

            // Add system message if not already there
            if (_history.Count == 0 || _history[0].role != AgentRole.System)
            {
                _history.Insert(0, AgentMessage.System(systemPrompt));
            }
            else
            {
                // Update system prompt with latest scene state
                _history[0] = AgentMessage.System(systemPrompt);
            }

            // Add user message
            var userMsg = AgentMessage.User(userGoal);
            _history.Add(userMsg);
            OnMessage?.Invoke(userMsg);

            // Get client and tools
            IGamePlanClient client = AiProviderSettings.CreateClient();
            var tools = EditorToolDispatcher.GetAllTools();
            int maxSteps = AiProviderSettings.AgentMaxSteps;

            for (int step = 1; step <= maxSteps; step++)
            {
                _cts.Token.ThrowIfCancellationRequested();

                if (_isPaused)
                {
                    while (_isPaused && !_cts.Token.IsCancellationRequested)
                        await Task.Delay(100);
                    if (_cts.Token.IsCancellationRequested) break;
                }

                OnStepProgress?.Invoke(step, maxSteps);

                // Send to AI
                AgentResponse response;
                try
                {
                    response = await client.GenerateAgentResponseAsync(_history, tools, _cts.Token);
                }
                catch (Exception ex)
                {
                    if (ex is TaskCanceledException || ex is OperationCanceledException)
                    {
                        if (UnityEditor.EditorApplication.isCompiling)
                        {
                            var reloadMsg = AgentMessage.System("Agent session was interrupted by a Unity Domain Reload (recompilation). Scripts were saved successfully. You may continue the conversation.");
                            _history.Add(reloadMsg);
                            OnMessage?.Invoke(reloadMsg);
                            break;
                        }
                    }

                    OnError?.Invoke(new Exception($"AI request failed (step {step}): {ex.Message}"));
                    break;
                }

                // Track tokens
                _totalPromptTokens += response.PromptTokens;
                _totalCompletionTokens += response.CompletionTokens;
                OnTokenUsage?.Invoke(_totalPromptTokens, _totalCompletionTokens);

                // Handle response
                if (response.HasToolCalls)
                {
                    // Process each tool call
                    foreach (var tc in response.ToolCalls)
                    {
                        // Record assistant's tool call message
                        var callMsg = AgentMessage.AssistantToolCall(tc.CallId, tc.ToolName, tc.Arguments?.ToString() ?? "{}");
                        _history.Add(callMsg);
                        OnMessage?.Invoke(callMsg);
                        OnToolCallStarted?.Invoke(tc.ToolName, tc.Arguments?.ToString() ?? "{}");

                        // Execute the tool
                        AgentToolResult result = EditorToolDispatcher.Dispatch(tc.ToolName, tc.Arguments ?? new JObject());

                        // Record tool result
                        var resultMsg = AgentMessage.ToolResult(tc.CallId, tc.ToolName, result.ToString());
                        _history.Add(resultMsg);
                        OnMessage?.Invoke(resultMsg);
                        OnToolCallCompleted?.Invoke(tc.ToolName, result);

                        if (!result.Success)
                            Debug.LogWarning($"Agent tool '{tc.ToolName}' failed: {result.Error}");
                    }

                    // Loop back to let the AI process the results
                    continue;
                }
                else if (response.HasText)
                {
                    // Agent replied with text — this is the final answer
                    var assistantMsg = AgentMessage.AssistantText(response.TextContent);
                    _history.Add(assistantMsg);
                    OnMessage?.Invoke(assistantMsg);

                    // Check if agent says it's done
                    OnDone?.Invoke();
                    return;
                }
                else
                {
                    // Empty response — treat as done
                    var emptyMsg = AgentMessage.AssistantText("(No response from AI)");
                    _history.Add(emptyMsg);
                    OnMessage?.Invoke(emptyMsg);
                    OnDone?.Invoke();
                    return;
                }
            }

            // Max steps reached
            var maxMsg = AgentMessage.AssistantText($"⚠️ Reached maximum steps ({AiProviderSettings.AgentMaxSteps}). Stopping. You can continue by sending another message.");
            _history.Add(maxMsg);
            OnMessage?.Invoke(maxMsg);
            OnDone?.Invoke();
        }
        catch (OperationCanceledException)
        {
            Debug.Log("Agent: Canceled.");
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
            Debug.LogError("Agent error: " + ex);
        }
        finally
        {
            _isRunning = false;
            _isPaused = false;
            if (ActiveOrchestrator == this)
                ActiveOrchestrator = null;

            ProcessGranularCompletion();
        }
    }

    private void ProcessGranularCompletion()
    {
        bool fileRenamed = false;
        foreach (var txtPath in _tempWrittenScripts)
        {
            if (System.IO.File.Exists(txtPath))
            {
                string csPath = txtPath.Substring(0, txtPath.Length - 4); // remove .txt
                System.IO.File.Move(txtPath, csPath);
                fileRenamed = true;
            }
        }
        _tempWrittenScripts.Clear();

        if (_queuedAttachments.Count > 0)
        {
            var wrapper = new PendingAttachmentWrapper { list = _queuedAttachments };
            UnityEditor.SessionState.SetString("AI_AGENT_PENDING_ATTACH", JsonUtility.ToJson(wrapper));
            UnityEditor.SessionState.SetBool("AI_AGENT_AWAITING_RELOAD", true);
            fileRenamed = true;
        }

        if (fileRenamed)
        {
            Debug.Log("[Agent] Finalizing scripted generation: Refreshing AssetDatabase...");
            UnityEditor.AssetDatabase.Refresh();
        }
    }

    [UnityEditor.InitializeOnLoadMethod]
    private static void BootstrapPostReloadAttachments()
    {
        UnityEditor.EditorApplication.delayCall += TryApplyQueuedAttachments;
    }

    private static void TryApplyQueuedAttachments()
    {
        if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
        {
            UnityEditor.EditorApplication.delayCall += TryApplyQueuedAttachments;
            return;
        }

        if (!UnityEditor.SessionState.GetBool("AI_AGENT_AWAITING_RELOAD", false)) return;

        string json = UnityEditor.SessionState.GetString("AI_AGENT_PENDING_ATTACH", "");
        UnityEditor.SessionState.SetBool("AI_AGENT_AWAITING_RELOAD", false);
        UnityEditor.SessionState.EraseString("AI_AGENT_PENDING_ATTACH");

        if (string.IsNullOrWhiteSpace(json)) return;

        var wrapper = JsonUtility.FromJson<PendingAttachmentWrapper>(json);
        if (wrapper == null || wrapper.list == null) return;

        MonoScriptResolver.RebuildIndex();

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var attach in wrapper.list)
        {
             var go = EditorToolDispatcher.FindInScene(scene, attach.objectId);
             if (go == null)
             {
                  Debug.LogWarning($"[Agent] Could not attach '{attach.scriptName}': target '{attach.objectId}' not found.");
                  continue;
             }
             var ms = MonoScriptResolver.Find(attach.scriptName);
             if (ms != null)
             {
                  var type = ms.GetClass();
                  if (type != null) UnityEditor.Undo.AddComponent(go, type);
             }
             else Debug.LogWarning($"[Agent] Failed compiling/finding '{attach.scriptName}' post-reload.");
        }
        Debug.Log("[Agent] Successfully finalized queued script attachments.");
    }

    public void Pause()
    {
        if (_isRunning)
            _isPaused = true;
    }

    public void Resume()
    {
        _isPaused = false;
    }

    public void Cancel()
    {
        _cts?.Cancel();
        _isRunning = false;
        _isPaused = false;
    }

    public void ClearHistory()
    {
        _history.Clear();
        _totalPromptTokens = 0;
        _totalCompletionTokens = 0;
    }

    public void LoadHistory(List<AgentMessage> messages)
    {
        _history = messages ?? new List<AgentMessage>();
    }

    public List<AgentMessage> GetHistory()
    {
        return new List<AgentMessage>(_history);
    }

    /// <summary>
    /// Allows external systems to inject a "tool result" message into the agent loop.
    /// Useful for compiler errors found after applying a code change.
    /// </summary>
    public void AddExternalResult(string sourceName, string content)
    {
        var msg = AgentMessage.ToolResult("external_" + Guid.NewGuid().ToString("N").Substring(0, 4), sourceName, content);
        _history.Add(msg);
        OnMessage?.Invoke(msg);
    }

    public void InjectSystemMessage(string content)
    {
        var msg = AgentMessage.System(content);
        _history.Add(msg);
        OnMessage?.Invoke(msg);
    }

    public async Task RunJsonFallbackAsync(string userGoal, GenerationConstraints constraints, bool editCurrentScene, bool isLevelGen)
    {
        if (_isRunning) throw new InvalidOperationException("Agent is already running.");
        ActiveOrchestrator = this;
        _isRunning = true;
        
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            var userMsg = AgentMessage.User(userGoal);
            _history.Add(userMsg);
            OnMessage?.Invoke(userMsg);

            IGamePlanClient client = AiProviderSettings.CreateClient();
            string prompt = BuildEffectiveJsonPrompt(userGoal, constraints, editCurrentScene, isLevelGen);
            
            OnStepProgress?.Invoke(1, 1);
            string json = await client.GeneratePlanAsync(prompt, _cts.Token);
            
            GameGenerationExecutor_Create.ValidateOnly(json);
            var parsedGen = JsonUtility.FromJson<GameGeneration>(json);

            var assistantMsg = AgentMessage.AssistantText("I have generated an Implementation Plan. Please review it in the confirmation window.");
            _history.Add(assistantMsg);
            OnMessage?.Invoke(assistantMsg);

            // Wait for user review
            PlanReviewWindow.Open(json, parsedGen, (approved) =>
            {
                if (!approved)
                {
                    var rejectMsg = AgentMessage.AssistantText("Plan rejected by user. Generation aborted.");
                    _history.Add(rejectMsg);
                    OnMessage?.Invoke(rejectMsg);
                    OnDone?.Invoke();
                    _isRunning = false;
                    return;
                }

                try
                {
                    if (parsedGen.scripts != null && parsedGen.scripts.Count > 0)
                    {
                        var scriptMsg = AgentMessage.AssistantText($"[Step 1] Compiling and generating {parsedGen.scripts.Count} script(s): \n- " + string.Join("\n- ", parsedGen.scripts.ConvertAll(s => s.name)));
                        _history.Add(scriptMsg);
                        OnMessage?.Invoke(scriptMsg);
                    }

                    int objCount = 0;
                    if (parsedGen.scenes != null)
                        foreach (var sc in parsedGen.scenes) objCount += sc.objects?.Count ?? 0;

                    var sceneMsg = AgentMessage.AssistantText($"[Step 2] Assembling the scene structure with {objCount} GameObjects...");
                    _history.Add(sceneMsg);
                    OnMessage?.Invoke(sceneMsg);
                    
                    if (isLevelGen)
                    {
                        if (editCurrentScene) LevelGenerationExecutor.ExecuteEdit(json);
                        else LevelGenerationExecutor.ExecuteCreate(json);
                    }
                    else
                    {
                        if (editCurrentScene) GameGenerationExecutor.ExecuteEdit(json);
                        else GameGenerationExecutor_Create.Execute(json); // or GameGenerationExecutor.ExecuteCreate
                    }

                    var successMsg = AgentMessage.ToolResult("json_eval", "System", "JSON execution successful. Scene is fully assembled.");
                    _history.Add(successMsg);
                    OnMessage?.Invoke(successMsg);

                    OnDone?.Invoke();
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                    var errMsg = AgentMessage.AssistantText($"Error executing JSON plan: {ex.Message}");
                    _history.Add(errMsg);
                    OnMessage?.Invoke(errMsg);
                }
                finally
                {
                    _isRunning = false;
                }
            });
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
            var errMsg = AgentMessage.AssistantText($"Error in JSON fallback: {ex.Message}");
            _history.Add(errMsg);
            OnMessage?.Invoke(errMsg);
            _isRunning = false;
        }
    }

    private string BuildEffectiveJsonPrompt(string userPrompt, GenerationConstraints constraints, bool editCurrentScene, bool isLevelGen)
    {
        var sb = new System.Text.StringBuilder();
        if (constraints != null) sb.AppendLine(constraints.ToPromptClause());

        if (isLevelGen)
        {
            sb.AppendLine("LEVELGEN MODE (FPS/Small Level):");
            sb.AppendLine("- Produce a SMALL playable FPS arena or compact interior.");
        }
        if (editCurrentScene)
        {
            sb.AppendLine("EDIT MODE:");
            sb.AppendLine("- Do NOT recreate the scene. Only output objects that must be added/updated.");
        }
        
        sb.AppendLine("USER REQUEST:");
        sb.AppendLine(userPrompt);

        return sb.ToString();
    }
}
