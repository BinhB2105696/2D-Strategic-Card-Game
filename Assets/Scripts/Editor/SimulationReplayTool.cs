#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Editor-only replay/export orchestration. It never changes the source trace, game rules or Player.
[InitializeOnLoad]
public static class SimulationReplayTool
{
    private const string Menu = "Card Game/Simulation/Replay and Export Latest AI Pilot";
    private static Process owned;
    private static string project, run, batch, executable, output, trace, log, report, dataset;
    private static string[] traces;
    private static readonly List<object> completed = new List<object>();
    private static Stopwatch totalTimer, childTimer;
    private static double nextPoll;
    private static long peakMemory;
    private static bool requestFaulted;
    public static bool IsRunning => batch != null;

    [Serializable] private sealed class PilotResource { public string status, runDirectory; }
    [Serializable] private sealed class VersionInfo { public int rulesVersion, catalogVersion; public string sourceFingerprint, unityVersion; }
    [Serializable] private sealed class ProgressInfo { public string state; public int completed, expected; }
    [Serializable] private sealed class ReplayReport
    {
        public bool verified, completeMatch, truncated;
        public string status, dataset, error, component;
        public int verifiedActions, verifiedDecisions;
    }

    static SimulationReplayTool()
    {
        EditorApplication.update += Poll;
        AssemblyReloadEvents.beforeAssemblyReload += StopForReload;
        EditorApplication.quitting += StopForReload;
    }

    [MenuItem(Menu, true)]
    private static bool CanRun() => !IsRunning && !SimulationPilotTool.IsRunning && !EditorApplication.isPlaying && !EditorApplication.isCompiling && !EditorApplication.isUpdating;

    [MenuItem(Menu)]
    public static void RunLatest()
    {
        if (!CanRun()) { Debug.LogWarning("Stop Play and wait for the active simulation/compilation first."); return; }
        string control = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Tools/Simulation/Control");
        foreach (string resource in Directory.GetFiles(control, "editor_pilot_*.resources.json").OrderByDescending(File.GetLastWriteTimeUtc))
        {
            PilotResource saved = JsonUtility.FromJson<PilotResource>(File.ReadAllText(resource));
            if (saved != null && saved.status == "completed") { Start(saved.runDirectory); return; }
        }
        Debug.LogError("No completed Editor AI pilot resource report was found.");
    }

    private static void Start(string directory)
    {
        try
        {
            project = Directory.GetParent(Application.dataPath).FullName;
            run = Path.GetFullPath(directory);
            string prefix = Path.Combine(project, "SimulationResults") + Path.DirectorySeparatorChar;
            if (!run.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Replay run must be inside SimulationResults.");
            var progress = JsonUtility.FromJson<ProgressInfo>(File.ReadAllText(Path.Combine(run, "progress.json")));
            if (progress == null || progress.state != "completed" || progress.completed != 12 || progress.expected != 12)
                throw new InvalidOperationException("Replay requires the completed 12-match pilot.");
            executable = Path.Combine(project, "Build/Simulation/CardGameSimulation.exe");
            VersionInfo build = JsonUtility.FromJson<VersionInfo>(File.ReadAllText(Path.Combine(project, "Build/Simulation/build-info.json")));
            VersionInfo recorded = JsonUtility.FromJson<VersionInfo>(File.ReadAllText(Path.Combine(run, "run.json")));
            if (!File.Exists(executable) || build == null || recorded == null || build.sourceFingerprint != recorded.sourceFingerprint ||
                build.rulesVersion != recorded.rulesVersion || build.catalogVersion != recorded.catalogVersion || build.unityVersion != Application.unityVersion)
                throw new InvalidOperationException("Current Player does not match the pilot; keep the matching runner instead of rebuilding over it.");
            traces = Directory.GetFiles(Path.Combine(run, "traces"), "*.jsonl").OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (traces.Length != 12) throw new InvalidOperationException("Expected exactly 12 pilot traces.");
            batch = Path.Combine(run, "replay", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_editor_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(batch); completed.Clear(); peakMemory = 0;
            totalTimer = Stopwatch.StartNew(); nextPoll = 0;
            Debug.Log("AI replay/export started: 12 source traces, one Player at a time. Source traces remain unchanged.\nOutput: " + batch);
            LaunchNext();
        }
        catch (Exception error) { if (batch != null) Finish("failed", error.ToString()); else Debug.LogError("Cannot start AI replay/export: " + error.Message); }
    }

    private static void LaunchNext()
    {
        trace = traces[completed.Count];
        output = Path.Combine(batch, Path.GetFileNameWithoutExtension(trace)); Directory.CreateDirectory(output);
        log = Path.Combine(output, "player.log"); report = Path.Combine(output, "report.json"); dataset = Path.Combine(output, "transitions.jsonl");
        string args = "-batchmode -nographics -logFile " + Quote(log) + " --simulation-project " + Quote(project) +
            " --replay-run " + Quote(run) + " --replay-trace " + Quote(trace) + " --replay-report " + Quote(report) + " --ai-export " + Quote(dataset);
        childTimer = Stopwatch.StartNew();
        owned = Process.Start(new ProcessStartInfo(executable, args) { WorkingDirectory = project,
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
        if (owned == null) throw new InvalidOperationException("Could not start replay Player.");
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1;
        if (!IsRunning)
        {
            // Same single-purpose file-request pattern as the build tool; no external commands are accepted.
            string request = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Tools/Simulation/Control/ai-replay-request.txt");
            if (!requestFaulted && CanRun() && File.Exists(request))
            {
                try
                {
                    string directory = File.ReadAllText(request).Trim();
                    File.Move(request, request + ".accepted_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    Start(directory);
                }
                catch (Exception error) { requestFaulted = true; Debug.LogError("Could not accept AI replay request; stopped retrying: " + error.Message); }
            }
            return;
        }
        try
        {
            owned.Refresh();
            if (!owned.HasExited) peakMemory = Math.Max(peakMemory, SimulationProcessResources.PeakWorkingSet(owned));
            if (totalTimer.Elapsed.TotalSeconds > 1800 || childTimer.Elapsed.TotalSeconds > 120 || peakMemory > 1536L * 1024 * 1024)
            { Finish("resource-limit", "Replay stopped at its time/RAM limit; keep reports and partial exports."); return; }
            if (Directory.GetFiles(batch, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length) > 1024L * 1024 * 1024)
            { Finish("resource-limit", "Replay output reached the 1024 MiB limit."); return; }
            if (!owned.HasExited) return;
            int exitCode = owned.ExitCode; owned.Dispose(); owned = null;
            ReplayReport verified = File.Exists(report) ? JsonUtility.FromJson<ReplayReport>(File.ReadAllText(report)) : null;
            if (exitCode != 0 || verified == null || !verified.verified || !verified.completeMatch || verified.truncated ||
                verified.status != "verified-match" || !File.Exists(dataset) || Path.GetFullPath(verified.dataset ?? "") != Path.GetFullPath(dataset))
            { Finish("failed", "Trace " + Path.GetFileName(trace) + " failed verification: " + (verified == null ? "no report; inspect " + log : verified.component + " " + verified.error)); return; }
            completed.Add(SimulationJson.Object("trace", Path.GetFileName(trace), "report", report, "dataset", dataset,
                "verifiedActions", verified.verifiedActions, "verifiedDecisions", verified.verifiedDecisions));
            Debug.Log("AI replay/export verified " + completed.Count + "/12: " + Path.GetFileName(trace));
            if (completed.Count == traces.Length) Finish("completed", "All 12 pilot traces replay-verified and exported. Dataset validation is still required before training.");
            else LaunchNext();
        }
        catch (Exception error) { Finish("failed", error.ToString()); }
    }

    [MenuItem("Card Game/Simulation/Stop AI Replay Export", true)]
    private static bool CanStop() => IsRunning;
    [MenuItem("Card Game/Simulation/Stop AI Replay Export")]
    public static void Stop() { if (IsRunning) Finish("stopped", "Replay/export stopped by user; completed reports retained."); }
    private static void StopForReload() { if (IsRunning) Finish("interrupted", "Replay/export stopped before Editor exit/script reload."); }

    private static void Finish(string status, string message)
    {
        try { if (owned != null && !owned.HasExited) { owned.Kill(); owned.WaitForExit(1000); } }
        catch (Exception error) { Debug.LogError("Could not stop owned replay: " + error.Message); }
        finally { owned?.Dispose(); owned = null; }
        totalTimer?.Stop(); childTimer?.Stop();
        string folder = batch; batch = null;
        try
        {
            if (folder != null) File.WriteAllText(Path.Combine(folder, "batch-report.json"), SimulationJson.Encode(SimulationJson.Object(
                "schema", 1, "status", status, "sourceRun", run, "verifiedMatches", completed.Count,
                "wallSeconds", totalTimer == null ? 0 : totalTimer.Elapsed.TotalSeconds,
                "peakRunnerWorkingSetBytes", peakMemory > 0 ? (object)peakMemory : null, "ramMeasurementAvailable", peakMemory > 0,
                "sampling", "One-second watchdog; process-memory fallback queries only owned child handles. Limits may overshoot between polls.",
                "results", completed, "message", message)));
        }
        catch (Exception error) { Debug.LogError("Could not save replay batch report: " + error.Message); }
        if (status == "completed") Debug.Log(message + "\nOutput: " + folder); else Debug.LogError(message + "\nOutput: " + folder);
    }

    private static string Quote(string value) => "\"" + value + "\"";
}
#endif
