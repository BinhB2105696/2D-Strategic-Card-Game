#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public static class SimulationPilotTool
{
    private const string RunMenu = "Card Game/Simulation/Run AI v2 Pilot";
    private const string StopMenu = "Card Game/Simulation/Stop AI Pilot";
    private static Process owned;
    private static Stopwatch timer;
    private static string progressPath, logPath, resourcePath, runDirectory, lastProgress;
    private static long peakWorkingSet, resultBytes;
    private static double nextPoll, nextSizeCheck;
    private static ProgressSnapshot latest;
    private static bool collectionRequestFaulted;
    public static bool IsRunning => owned != null;

    [Serializable] private sealed class ProgressSnapshot
    {
        public string state, activeMatch, runDirectory, error;
        public int completed, expected;
    }
    [Serializable] private sealed class BuildInfo
    {
        public string result, unityVersion, collectionConfigHash;
        public int aiSchema, rulesVersion, catalogVersion, deckCount;
    }

    static SimulationPilotTool()
    {
        EditorApplication.update += Poll;
        AssemblyReloadEvents.beforeAssemblyReload += StopForEditorExit;
        EditorApplication.quitting += StopForEditorExit;
    }

    [MenuItem(RunMenu, true)]
    private static bool CanRun() => owned == null && !SimulationReplayTool.IsRunning && !EditorApplication.isPlaying && !EditorApplication.isCompiling && !EditorApplication.isUpdating;

    [MenuItem(RunMenu)]
    public static void Run() => RunConfig("ai-v2-pilot.json");

    [MenuItem("Card Game/Simulation/Run AI Collection Pilot", true)]
    private static bool CanRunCollection() => CanRun();
    [MenuItem("Card Game/Simulation/Run AI Collection Pilot")]
    public static void RunCollection()
    {
        if (!CanRun()) { Debug.LogWarning("Wait for the current simulation or compilation first."); return; }
        if (!CollectionBuildReady()) { Debug.LogError("Build the current collection runner first."); return; }
        string request = CollectionRequestPath();
        if (File.Exists(request)) File.Move(request, request + ".accepted_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        RunConfig("ai-v2-collection-pilot.json");
    }

    [MenuItem("Card Game/Simulation/Run AI Hard Teacher Pilot", true)]
    private static bool CanRunHardTeacher() => CanRun();
    [MenuItem("Card Game/Simulation/Run AI Hard Teacher Pilot")]
    public static void RunHardTeacher() => RunConfig("ai-v2-hard-teacher-pilot.json");

    private static string CollectionRequestPath() => Path.Combine(Directory.GetParent(Application.dataPath).FullName,
        "Tools/Simulation/Control/ai-collection-request.txt");

    private static bool CollectionBuildReady()
    {
        string project = Directory.GetParent(Application.dataPath).FullName;
        string infoPath = Path.Combine(project, "Build/Simulation/build-info.json");
        string collectionPath = Path.Combine(project, "Tools/Simulation/collection-decks-v1.json");
        if (!File.Exists(infoPath) || !File.Exists(collectionPath)) return false;
        BuildInfo info = JsonUtility.FromJson<BuildInfo>(File.ReadAllText(infoPath));
        return info != null && info.result != null && info.result.StartsWith("Succeeded", StringComparison.Ordinal) &&
            info.unityVersion == Application.unityVersion && info.aiSchema == SimulationAiContract.Schema &&
            info.rulesVersion == MatchHeader.CurrentRulesVersion && info.catalogVersion == MatchHeader.CurrentCardCatalogVersion &&
            info.deckCount >= 5 && info.collectionConfigHash == SimulationJson.Hash(File.ReadAllText(collectionPath));
    }

    private static void RunConfig(string configName)
    {
        if (!CanRun()) { Debug.LogWarning("Stop Play Mode and wait for compilation or the current pilot to finish."); return; }
        string project = Directory.GetParent(Application.dataPath).FullName;
        string executable = Path.Combine(project, "Build/Simulation/CardGameSimulation.exe");
        string infoPath = Path.Combine(project, "Build/Simulation/build-info.json");
        if (!File.Exists(executable) || !File.Exists(infoPath)) { Debug.LogError("Build Headless Windows Runner first."); return; }
        BuildInfo info = JsonUtility.FromJson<BuildInfo>(File.ReadAllText(infoPath));
        if (info == null || info.result == null || !info.result.StartsWith("Succeeded", StringComparison.Ordinal) ||
            info.unityVersion != Application.unityVersion || info.aiSchema != SimulationAiContract.Schema ||
            info.rulesVersion != MatchHeader.CurrentRulesVersion || info.catalogVersion != MatchHeader.CurrentCardCatalogVersion)
        { Debug.LogError("Runner metadata does not match the current versions. Rebuild the headless runner first."); return; }
        string configPath = Path.Combine(project, "Tools/Simulation", configName);
        SimulationConfig config = JsonUtility.FromJson<SimulationConfig>(File.ReadAllText(configPath));
        if (config == null || config.matchups == null || config.seedCount < 1 || config.detailedMatches != 12 ||
            !string.Equals(Path.GetFullPath(config.projectRoot ?? ""), project, StringComparison.OrdinalIgnoreCase) ||
            config.matchups.Sum(matchup => config.seedCount * (matchup.swapBots ? 2 : 1)) != 12 ||
            config.matchups.Any(matchup => matchup.deck0 != matchup.deck1))
        { Debug.LogError("Expected the 12-match pilot configuration for this project with equal decks on both seats."); return; }
        if (config.matchups.Any(matchup => matchup.deck0 >= 3))
        {
            string collectionPath = Path.Combine(project, "Tools/Simulation/collection-decks-v1.json");
            if (info.deckCount < 5 || !File.Exists(collectionPath) ||
                info.collectionConfigHash != SimulationJson.Hash(File.ReadAllText(collectionPath)))
            { Debug.LogError("Rebuild the headless runner to include the current simulation-only collection decks."); return; }
        }
        string control = Path.Combine(project, "Tools/Simulation/Control");
        Directory.CreateDirectory(control);
        string id = "editor_pilot_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        progressPath = Path.Combine(control, id + ".json"); logPath = Path.Combine(control, id + ".log");
        resourcePath = Path.Combine(control, id + ".resources.json");
        runDirectory = null; lastProgress = null; latest = null;
        peakWorkingSet = resultBytes = 0; nextPoll = nextSizeCheck = 0;
        timer = Stopwatch.StartNew();
        try
        {
            owned = Process.Start(new ProcessStartInfo(executable,
                "-batchmode -nographics -logFile " + Quote(logPath) + " --simulation-config " + Quote(configPath) +
                " --simulation-progress " + Quote(progressPath)) {
                WorkingDirectory = project, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
            if (owned == null) throw new InvalidOperationException("Could not start the headless Player.");
            Debug.Log("AI pilot started: 12 recorded matches + 2 warmups. Limits: 30 minutes, runner RAM 1536 MiB, output 1024 MiB.\nPlayer log: " + logPath);
        }
        catch (Exception error) { Finish("failed", "Could not launch AI pilot: " + error.Message); }
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1;
        if (owned == null)
        {
            if (!collectionRequestFaulted && CanRun() && File.Exists(CollectionRequestPath()))
                try { if (CollectionBuildReady()) RunCollection(); }
                catch (Exception error) { collectionRequestFaulted = true; Debug.LogError("Could not accept collection request; stopped retrying: " + error.Message); }
            return;
        }
        try
        {
            owned.Refresh();
            if (!owned.HasExited) peakWorkingSet = Math.Max(peakWorkingSet, SimulationProcessResources.PeakWorkingSet(owned));
            if (timer.Elapsed.TotalSeconds > 1800 || peakWorkingSet > 1536L * 1024 * 1024)
            { Finish("resource-limit", "AI pilot stopped at the time/RAM limit. Partial results retained; this is not a game loss."); return; }
            if (File.Exists(progressPath))
            {
                using (var stream = new FileStream(progressPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream)) latest = JsonUtility.FromJson<ProgressSnapshot>(reader.ReadToEnd());
                if (latest != null)
                {
                    if (!string.IsNullOrEmpty(latest.runDirectory))
                    {
                        string candidate = Path.GetFullPath(latest.runDirectory);
                        string prefix = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "SimulationResults") + Path.DirectorySeparatorChar;
                        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected pilot output location.");
                        runDirectory = candidate;
                    }
                    string text = latest.state + ": " + latest.completed + "/" + latest.expected + " " + latest.activeMatch;
                    if (text != lastProgress) { Debug.Log("AI pilot " + text); lastProgress = text; }
                }
            }
            if (EditorApplication.timeSinceStartup >= nextSizeCheck)
            {
                nextSizeCheck = EditorApplication.timeSinceStartup + 5;
                MeasureOutput();
            }
            if (timer.Elapsed.TotalSeconds > 1800 || peakWorkingSet > 1536L * 1024 * 1024 || resultBytes > 1024L * 1024 * 1024)
            { Finish("resource-limit", "AI pilot stopped at a resource limit. Keep partial results; this is not a game loss."); return; }
            if (owned.HasExited)
            {
                int code = owned.ExitCode;
                bool complete = code == 0 && latest != null && latest.completed == 12 && latest.expected == 12;
                string error = latest != null && !string.IsNullOrEmpty(latest.error) ? latest.error : ReadPlayerFailure();
                Finish(complete ? "completed" : "failed", complete ? "AI pilot completed: 12/12. Replay/export verification is the next step."
                    : "AI pilot failed (exit " + code + "). " + error);
            }
        }
        catch (IOException error)
        {
            // A replacement can race this one-second read; retry while the process is alive.
            if (owned != null && owned.HasExited) Finish("failed", "Could not read pilot progress: " + error.Message);
        }
        catch (Exception error) { Finish("failed", "AI pilot monitor failed: " + error.Message); }
    }

    [MenuItem(StopMenu, true)]
    private static bool CanStop() => owned != null;
    [MenuItem(StopMenu)]
    public static void Stop() { if (owned != null) Finish("stopped", "AI pilot stopped by user; partial results retained."); }
    private static void StopForEditorExit() { if (owned != null) Finish("interrupted", "AI pilot stopped before Editor exit/script reload; partial results retained."); }

    private static void MeasureOutput()
    {
        resultBytes = !string.IsNullOrEmpty(runDirectory) && Directory.Exists(runDirectory)
            ? Directory.GetFiles(runDirectory, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length) : 0;
        if (File.Exists(logPath)) resultBytes += new FileInfo(logPath).Length;
    }

    private static string ReadPlayerFailure()
    {
        try
        {
            using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
                for (int i = 0; i < 2000; i++)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;
                    if (line.Contains("Exception:")) return line;
                }
        }
        catch (IOException) { }
        return "Read the Player log for the original exception.";
    }

    private static void Finish(string status, string message)
    {
        Process process = owned; owned = null;
        try { if (process != null && !process.HasExited) { process.Kill(); process.WaitForExit(1000); } }
        catch (Exception error) { Debug.LogError("Could not stop the owned pilot process: " + error.Message); }
        finally { process?.Dispose(); }
        timer?.Stop();
        try
        {
            MeasureOutput();
            File.WriteAllText(resourcePath, SimulationJson.Encode(SimulationJson.Object("schema", 1, "status", status,
                "runDirectory", runDirectory, "wallSeconds", timer == null ? 0 : timer.Elapsed.TotalSeconds,
                "peakRunnerWorkingSetBytes", peakWorkingSet > 0 ? (object)peakWorkingSet : null,
                "ramMeasurementAvailable", peakWorkingSet > 0, "outputBytesIncludingPlayerLog", resultBytes,
                "maxBatchSeconds", 1800, "maxWorkingSetMiB", 1536, "maxResultsMiB", 1024,
                "sampling", "Runner RAM polled each second; output each five seconds. Limits may overshoot between polls; whole-system free RAM is not measured.",
                "playerLog", logPath)));
        }
        catch (Exception error) { Debug.LogError("Could not save pilot resource report: " + error.Message); }
        string detail = message + "\nResults: " + (runDirectory ?? "not created") + "\nPlayer log: " + logPath + "\nResources: " + resourcePath;
        if (status == "completed") Debug.Log(detail); else if (status == "stopped" || status == "interrupted") Debug.LogWarning(detail); else Debug.LogError(detail);
    }

    private static string Quote(string value) => "\"" + value + "\"";
}
#endif
