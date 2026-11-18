using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SimulationBuildTool
{
    private const string Stage = "CardGame.Simulation.BuildStage";
    private const string ScenePath = "Assets/Simulation/SimulationRunner.unity";
    private static readonly string Control = Path.GetFullPath("Tools/Simulation");
    private static double nextPoll;
    private static double targetStableSince;
    [Serializable] private sealed class CollectionDeckDocument
    {
        public int schemaVersion;
        public SavedDeck[] decks;
    }
    static SimulationBuildTool() { EditorApplication.update += Tick; }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        if (SessionState.GetString(Stage, "") == "waiting-build-compilation")
            SessionState.SetString(Stage, "ready-to-build");
    }

    [MenuItem("Card Game/Simulation/Build Headless Windows Runner")]
    public static void RequestBuild()
    {
        Directory.CreateDirectory(Control);
        File.WriteAllText(Path.Combine(Control, "editor-request.txt"), "build|" + Path.GetFullPath("."));
    }

    [MenuItem("Card Game/Simulation/Export Latest Build Diagnostics")]
    public static void ExportLatestBuildDiagnostics()
    {
        SimulationJson.ResultsRoot(Path.GetFullPath("."));
        BuildReport report = BuildReport.GetLatestReport();
        if (report == null) throw new InvalidOperationException("No previous Unity build report is available.");
        string reportDirectory = Path.Combine(Control, "Control");
        Directory.CreateDirectory(reportDirectory);
        var messages = report.steps.SelectMany(step => step.messages
            .Where(message => message.type == LogType.Warning || message.type == LogType.Error || message.type == LogType.Exception)
            .Select(message => SimulationJson.Object("step", step.name, "type", message.type.ToString(), "message", message.content))).ToArray();
        SimulationJson.Atomic(Path.Combine(reportDirectory, "latest-build-report.json"), SimulationJson.Object(
            "result", report.summary.result.ToString(), "errors", report.summary.totalErrors,
            "warnings", report.summary.totalWarnings, "messages", messages));
        Debug.Log("Latest build diagnostics exported to Tools/Simulation/Control/latest-build-report.json");
    }

    public static void PrepareAndBuildRunnerFromCommandLine()
    {
        CheckReady();
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new InvalidOperationException("Cannot switch to the Windows build target.");
        SimulationVerification.Verify();
        ReplayFoundationVerification.Verify();
        PrepareScene();
        Build();
        ExportLatestBuildDiagnostics();
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { targetStableSince = 0; return; }
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1;
        try
        {
            string stage = SessionState.GetString(Stage, "");
            if (stage == "waiting-build-compilation")
            {
                if (EditorUtility.scriptCompilationFailed)
                    throw new InvalidOperationException("Editor script compilation failed. Resolve the Console errors before retrying the simulator build.");
                return;
            }
            if (stage == "ready-to-build")
            {
                CheckReady();
                SessionState.SetString(Stage, "");
                Build();
                return;
            }
            if (SessionState.GetString(Stage, "") == "waiting-target")
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64) return;
                // The target can change before Unity queues its final import/reload.
                // Build only after a stable idle interval, not on that first tick.
                if (targetStableSince == 0) targetStableSince = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - targetStableSince < 5) return;
                Status("compiling", "Refreshing changed files and recompiling Editor scripts; build resumes only after a successful assembly reload.");
                SessionState.SetString(Stage, "waiting-build-compilation");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                // Force a real compilation/reload even when Unity reports no changed assemblies.
                CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
                return;
            }
            string requestPath = Path.Combine(Control, "editor-request.txt");
            if (!File.Exists(requestPath)) return;
            string[] request = File.ReadAllText(requestPath).Split(new[] { '|' }, 2);
            if (request.Length != 2 || !string.Equals(Path.GetFullPath(request[1]), Path.GetFullPath("."), StringComparison.OrdinalIgnoreCase)) return;
            File.Delete(requestPath);
            CheckReady();
            if (request[0] == "preflight") { Status("ready", "Editor compiled; simulator preparation can proceed."); return; }
            if (request[0] == "retry")
            {
                ValidatePreparedCatalog();
                Status("retrying", "Retrying the prepared build; passed runtime checks are not repeated.");
            }
            else
            {
                if (request[0] != "build") throw new InvalidOperationException("Unknown simulation build request.");
                Status("verifying", "Verifying runner without loading duel UI.");
                SimulationVerification.Verify();
            ReplayFoundationVerification.Verify();
                PrepareScene();
            }
            targetStableSince = 0;
            SessionState.SetString(Stage, "waiting-target");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                Status("switching", "Switching Editor to Windows for the separate headless runner.");
                if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                    throw new InvalidOperationException("Cannot switch to Windows build target.");
            }
        }
        catch (Exception exception)
        {
            SessionState.SetString(Stage, ""); Status("blocked", exception.ToString()); Debug.LogException(exception);
        }
    }

    private static void CheckReady()
    {
        SimulationJson.ResultsRoot(Path.GetFullPath("."));
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before preparing the simulator.");
        if (EditorUtility.scriptCompilationFailed) throw new InvalidOperationException("Resolve script compilation errors first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the scene first: " + SceneManager.GetSceneAt(i).path);
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new InvalidOperationException("Windows Build Support is required.");
    }

    private static void PrepareScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Simulation")) AssetDatabase.CreateFolder("Assets", "Simulation");
        string path = "Assets/Simulation/SimulationCatalog.asset";
        SimulationCatalog asset = AssetDatabase.LoadAssetAtPath<SimulationCatalog>(path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<SimulationCatalog>(); AssetDatabase.CreateAsset(asset, path); }
        asset.cards = LoadCatalogCards();
        var catalog = asset.cards.ToDictionary(card => card.CardId);
        asset.presets = CreateSimulationPresets(catalog);
        foreach (SavedDeck deck in asset.presets)
            if (!LocalDeckStore.Validate(deck, catalog, GameConfig.CreateDefault(), out string error)) throw new InvalidOperationException(error);
        foreach (CardData card in asset.cards) CardDataConverter.ToDefinition(card);
        asset.sourceRevision = Revision();
        asset.sourceFingerprint = SourceFingerprint();
        asset.catalogFingerprint = CatalogFingerprint(asset.cards);
        EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
        Scene previous = SceneManager.GetActiveScene();
        bool restorePrevious = previous.IsValid() && !string.IsNullOrEmpty(previous.path);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            restorePrevious ? NewSceneMode.Additive : NewSceneMode.Single);
        try
        {
            var root = new GameObject("SimulationRunner"); SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<SimulationRunner>().catalog = asset;
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (restorePrevious) SceneManager.SetActiveScene(previous);
        }
    }

    private static void Build()
    {
        CheckReady();
        ValidatePreparedCatalog();
        string output = Path.GetFullPath("Build/Simulation/CardGameSimulation.exe");
        SimulationRunnerArchive.Preserve(Path.GetDirectoryName(output));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        Status("building", "Building simulator-only Windows Player; no board, HUD or AppShell scenes included.");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },
            target = BuildTarget.StandaloneWindows64, locationPathName = output, options = BuildOptions.None });
        string[] errors = report.steps.SelectMany(step => step.messages).Where(message => message.type == LogType.Error || message.type == LogType.Exception)
            .Select(message => message.content).Take(10).ToArray();
        string[] warnings = report.steps.SelectMany(step => step.messages).Where(message => message.type == LogType.Warning)
            .Select(message => message.content).Take(20).ToArray();
        string messageText = report.summary.result + "; errors=" + report.summary.totalErrors + "; warnings=" + report.summary.totalWarnings;
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(messageText + "\n" + string.Join("\n", errors));
        SimulationJson.Atomic(Path.Combine(Path.GetDirectoryName(output), "build-info.json"), SimulationJson.Object("utc", DateTime.UtcNow.ToString("o"),
            "result", messageText, "warnings", warnings, "sourceRevision", Revision(), "sourceFingerprint", SourceFingerprint(),
            "rulesVersion", MatchHeader.CurrentRulesVersion, "catalogVersion", MatchHeader.CurrentCardCatalogVersion,
            "aiSchema", SimulationAiContract.Schema, "unityVersion", Application.unityVersion,
            "runtime", Environment.Version.ToString(),
            "deckCount", AssetDatabase.LoadAssetAtPath<SimulationCatalog>("Assets/Simulation/SimulationCatalog.asset").presets.Length,
            "collectionConfigHash", CollectionConfigHash()));
        Status("succeeded", messageText + ". Run Tools/Simulation/Run-Simulation.ps1. Results stay on D.");
    }

    private static CardData[] LoadCatalogCards() => AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data/Cards" })
        .Select(guid => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid)))
        .Where(card => card != null).OrderBy(card => card.CardId, StringComparer.Ordinal).ToArray();

    private static string CatalogFingerprint(CardData[] cards) => SimulationJson.Hash(string.Join("\n", cards.Select(card =>
        card.CardId + ":" + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(card)))));

    private static void ValidatePreparedCatalog()
    {
        var prepared = AssetDatabase.LoadAssetAtPath<SimulationCatalog>("Assets/Simulation/SimulationCatalog.asset");
        CardData[] cards = LoadCatalogCards();
        if (prepared == null || !File.Exists(ScenePath) || prepared.sourceFingerprint != SourceFingerprint() ||
            prepared.catalogFingerprint != CatalogFingerprint(cards) ||
            DeckFingerprint(prepared.presets) != DeckFingerprint(CreateSimulationPresets(cards.ToDictionary(card => card.CardId))))
            throw new InvalidOperationException("Runtime code or card data changed after preparation. Run the normal simulator build to prepare current data.");
    }

    private static SavedDeck[] CreateSimulationPresets(IReadOnlyDictionary<string, CardData> catalog)
    {
        var presets = LocalDeckStore.CreatePreviewProfile(catalog).decks.Select((deck, i) =>
            new SavedDeck { deckId = "simulation_preset_" + i, name = deck.name, cardIds = deck.cardIds.ToList() }).ToList();
        string path = Path.Combine(Control, "collection-decks-v1.json");
        if (File.Exists(path))
        {
            var additions = JsonUtility.FromJson<CollectionDeckDocument>(File.ReadAllText(path, Encoding.UTF8));
            if (additions == null || additions.schemaVersion != 1 || additions.decks == null || additions.decks.Length != 2 ||
                additions.decks.Any(deck => deck == null || string.IsNullOrEmpty(deck.deckId) || !deck.deckId.StartsWith("collection_", StringComparison.Ordinal)))
                throw new InvalidOperationException("Invalid simulation-only collection deck configuration.");
            presets.AddRange(additions.decks);
        }
        if (presets.Select(deck => deck.deckId).Distinct(StringComparer.Ordinal).Count() != presets.Count)
            throw new InvalidOperationException("Simulation deck IDs must be unique.");
        return presets.ToArray();
    }

    private static string DeckFingerprint(SavedDeck[] decks) => SimulationJson.Hash(string.Join("\n",
        decks.Select(deck => deck.deckId + ":" + deck.name + ":" + string.Join(",", deck.cardIds))));

    private static string CollectionConfigHash()
    {
        string path = Path.Combine(Control, "collection-decks-v1.json");
        return File.Exists(path) ? SimulationJson.Hash(File.ReadAllText(path, Encoding.UTF8)) : "";
    }

    private static string SourceFingerprint()
    {
        string[] roots = { "Assets/Scripts/Core", "Assets/Scripts/Bots", "Assets/Scripts/Simulation", "Assets/Scripts/Effects" };
        var paths = roots.SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .Concat(new[] { "Assets/Scripts/CardData.cs", "Assets/Scripts/CardEffectData.cs", "Assets/Scripts/App/LocalDeckStore.cs", "Assets/Scripts/App/AppProfileServices.cs" })
            .OrderBy(path => path, StringComparer.Ordinal);
        return SimulationJson.Hash(string.Join("\n", paths.Select(path => path.Replace('\\', '/') + ":" + SimulationJson.Hash(File.ReadAllText(path, Encoding.UTF8)))));
    }

    private static string Revision()
    {
        try
        {
            using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", "rev-parse HEAD") {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }))
            { string result = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit(); return process.ExitCode == 0 ? result : "unknown"; }
        }
        catch { return "unknown"; }
    }

    private static void Status(string state, string message)
    {
        Directory.CreateDirectory(Control);
        SimulationJson.Atomic(Path.Combine(Control, "build-status.json"), SimulationJson.Object("state", state, "message", message, "utc", DateTime.UtcNow.ToString("o")));
    }
}
