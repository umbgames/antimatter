using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[InitializeOnLoad]
public static class AutoFixCompilerErrors
{
    private const string SessionGuard = "AI_GAMEGEN_AUTOFIX_IN_PROGRESS";
    private const string SessionPassCount = "AI_GAMEGEN_AUTOFIX_PASSCOUNT";

    // Window can read this
    public static string Status { get; private set; } = "";

    private static CompilerMessage[] _pendingErrors;

    static AutoFixCompilerErrors()
    {
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
    }

    public static bool IsEnabled
    {
        get => EditorPrefs.GetBool(AiPrefsKeys.AutoFixEnabled, false);
        set => EditorPrefs.SetBool(AiPrefsKeys.AutoFixEnabled, value);
    }

    public static int MaxPasses
    {
        get => EditorPrefs.GetInt(AiPrefsKeys.AutoFixMaxPasses, 3);
        set => EditorPrefs.SetInt(AiPrefsKeys.AutoFixMaxPasses, Mathf.Clamp(value, 1, 10));
    }

    public static void ResetPassCounter()
    {
        SessionState.SetInt(SessionPassCount, 0);
        Status = "";
    }

    private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
        if (!IsEnabled) return;
        if (SessionState.GetBool(SessionGuard, false)) return;

        var errors = messages?.Where(m => m.type == CompilerMessageType.Error).ToArray();
        if (errors == null || errors.Length == 0) return;

        _pendingErrors = errors;
        Status = "Error detected. Auto-repairing…";
        Debug.Log("AI Auto-fix: Error detected. Auto-repairing…");

        EditorApplication.delayCall -= TryFixPending;
        EditorApplication.delayCall += TryFixPending;
    }

    private static void TryFixPending()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall -= TryFixPending;
            EditorApplication.delayCall += TryFixPending;
            return;
        }

        EditorApplication.delayCall -= TryFixPending;

        if (NotifyAgentOfErrors(_pendingErrors))
        {
            _pendingErrors = null;
            return;
        }

        _ = FixPendingErrorsAsync();
    }

    private static async Task FixPendingErrorsAsync()
    {
        if (_pendingErrors == null || _pendingErrors.Length == 0) return;
        if (!IsEnabled) return;
        if (SessionState.GetBool(SessionGuard, false)) return;

        int passCount = SessionState.GetInt(SessionPassCount, 0);
        int maxPasses = MaxPasses;

        if (passCount >= maxPasses)
        {
            Debug.LogWarning($"AI Auto-fix: Max passes reached ({maxPasses}). Disabling auto-fix to prevent infinite loops.");
            IsEnabled = false;
            Status = "Auto-fix disabled (max passes reached).";
            return;
        }

        SessionState.SetBool(SessionGuard, true);
        SessionState.SetInt(SessionPassCount, passCount + 1);

        try
        {
            var grouped = _pendingErrors
                .Where(e => !string.IsNullOrWhiteSpace(e.file))
                .GroupBy(e => e.file)
                .OrderByDescending(g => g.Count())
                .ToList();

            if (grouped.Count == 0)
            {
                Status = "Auto-fix: errors had no file path. Can't patch.";
                Debug.LogWarning(Status);
                return;
            }

            var target = grouped[0];
            string filePath = NormalizeToProjectPath(target.Key);

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Status = $"Auto-fix: file not found: {target.Key}";
                Debug.LogWarning(Status);
                return;
            }

            if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                Status = $"Auto-fix: not a .cs file: {filePath}";
                Debug.LogWarning(Status);
                return;
            }

            string code = File.ReadAllText(filePath);
            string errorText = FormatErrors(target.ToArray());

            string prompt =
                "You are fixing Unity C# compiler errors.\n" +
                "Return ONLY the full corrected C# file content.\n" +
                "No markdown. No commentary.\n" +
                "Do NOT change unrelated behavior.\n" +
                "Keep class names and public APIs unless required to fix compilation.\n\n" +
                $"FILE: {filePath}\n\n" +
                "ERRORS:\n" + errorText + "\n\n" +
                "FULL FILE:\n" + code;

            IGamePlanClient client = AiProviderSettings.CreateClient();

            EditorUtility.DisplayProgressBar("AI Auto-fix", "Error detected. Auto-repairing…", 0.35f);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            string fixedFile = await client.GeneratePlanAsync(prompt, cts.Token);

            if (string.IsNullOrWhiteSpace(fixedFile))
                throw new Exception("AI Auto-fix: model returned empty output.");

            if (!fixedFile.Contains("class") || fixedFile.TrimStart().StartsWith("{"))
                throw new Exception("AI Auto-fix: output doesn't look like a C# file.");

            string backupPath = filePath + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            File.Copy(filePath, backupPath, overwrite: false);
            File.WriteAllText(filePath, fixedFile, Encoding.UTF8);

            Debug.Log($"AI Auto-fix: Patched {filePath}\nBackup: {backupPath}");

            _pendingErrors = null;
            Status = "Auto-fix applied. Recompiling…";

            EditorUtility.DisplayProgressBar("AI Auto-fix", "Refreshing + recompiling…", 0.85f);
            AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            Status = "Auto-fix failed. Check Console.";
            Debug.LogError("AI Auto-fix failed:\n" + ex);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            SessionState.SetBool(SessionGuard, false);
        }
    }

    private static bool NotifyAgentOfErrors(CompilerMessage[] errs)
    {
        var agent = AgentOrchestrator.ActiveOrchestrator;
        if (agent == null || !agent.IsRunning) return false;

        string formatted = FormatErrors(errs);
        agent.AddExternalResult("compiler_errors", "COMPILER ERRORS DETECTED:\n" + formatted);
        return true;
    }

    private static string NormalizeToProjectPath(string compilerPath)
    {
        string p = compilerPath.Replace("\\", "/");
        if (p.StartsWith("Assets/")) return p;

        string project = Directory.GetParent(Application.dataPath)?.FullName?.Replace("\\", "/");
        if (!string.IsNullOrWhiteSpace(project) && p.StartsWith(project))
        {
            string rel = p.Substring(project.Length + 1);
            return rel.Replace("\\", "/");
        }
        return p;
    }

    private static string FormatErrors(CompilerMessage[] errs)
    {
        var sb = new StringBuilder();
        foreach (var e in errs.Take(20))
            sb.AppendLine($"- {Path.GetFileName(e.file)}:{e.line}:{e.column} {e.message}");
        if (errs.Length > 20) sb.AppendLine($"(and {errs.Length - 20} more)");
        return sb.ToString();
    }
}
