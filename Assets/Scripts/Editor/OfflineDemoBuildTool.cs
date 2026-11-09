using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit file/menu request only: never build merely because scripts reload.
[InitializeOnLoad]
public static class OfflineDemoBuildTool
{
    private const string StageKey = "CardGame.OfflineDemo.BuildStage";
    private const string Output = "Build/OfflineDemo/CardGame-OfflineDemo-0.1.0.apk";
    private static readonly string Request = Path.Combine(Path.GetTempPath(), "card-game-offline-demo.request");
    private static readonly string Status = Path.Combine(Path.GetTempPath(), "card-game-offline-demo.status.json");
    private static readonly string[] Scenes = { "Assets/Scenes/AppShell.unity", "Assets/Scenes/SampleScene.unity" };
    private static double nextPoll;

    [Serializable]
    private sealed class Progress
    {
        public string state, message, output, utc;
    }

    static OfflineDemoBuildTool() { EditorApplication.update += Tick; }

    [MenuItem("Card Game/App Shell/Build Offline Demo APK")]
    public static void RequestBuild()
    {
        File.WriteAllText(Request, "build|" + Path.GetFullPath("."));
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + 1;
        try
        {
            string stage = SessionState.GetString(StageKey, "");
            if (stage == "waiting-target")
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) return;
                SessionState.SetString(StageKey, "");
                Build();
                return;
            }
            if (!File.Exists(Request)) return;
            string request = File.ReadAllText(Request);
            string[] parts = request.Split(new[] { '|' }, 2);
            if (parts.Length != 2 || !string.Equals(Path.GetFullPath(parts[1]), Path.GetFullPath("."), StringComparison.OrdinalIgnoreCase)) return;
            File.Delete(Request);
            CheckReady();
            if (parts[0] == "preflight")
            {
                Write("ready", "Editor is ready. Saved scenes and Android module are present; no scenes were saved or rebuilt.");
                return;
            }
            if (parts[0] != "build") throw new InvalidOperationException("Unknown demo build request.");
            VerifyLocalStorage();
            Write("configuring", "Local save checks passed. Configuring portrait APK for ARMv7 + ARM64.");
            Configure();
            SessionState.SetString(StageKey, "waiting-target");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Write("switching", "Switching this Editor to Android. Asset import and script reload may take several minutes.");
                if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android))
                    throw new InvalidOperationException("Unity could not start switching to Android.");
            }
        }
        catch (Exception exception)
        {
            SessionState.SetString(StageKey, "");
            Write("blocked", exception.Message);
            Debug.LogException(exception);
        }
    }

    private static void CheckReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode, then request the demo build again.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isDirty) throw new InvalidOperationException("Save the scene before building: " + scene.path);
        }
        foreach (string scene in Scenes)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                throw new InvalidOperationException("Missing build scene: " + scene);
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new InvalidOperationException("Android Build Support is missing from this Unity installation.");
        if (EditorUtility.scriptCompilationFailed)
            throw new InvalidOperationException("Fix Unity compilation errors before building.");
    }

    private static void Configure()
    {
        // Keep company/product names: desktop local saves must retain their path.
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.binhb2105696.strategiccardgame.demo");
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)23;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
        PlayerSettings.Android.forceInternetPermission = false;
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
    }

    private static void VerifyLocalStorage()
    {
        var catalog = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data/Cards" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(card => card != null).ToDictionary(card => card.CardId);
        string scratch = Path.Combine(Path.GetTempPath(), "CardGameDemoSaveCheck", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        string path = Path.Combine(scratch, "profile.json");
        LocalPlayerProfile profile = LocalDeckStore.Load(catalog, out string warning, path);
        if (warning != null || profile.decks.Count < 2) throw new InvalidOperationException("Starter profile/decks failed validation: " + warning);
        profile.guestName = "Offline Demo Tester";
        profile.settings.masterVolume = 0.6f;
        profile.recentMatches.Add(new LocalMatchSummary { matchId = "demo-storage-check", outcome = "Win", opponent = "Normal Bot" });
        if (!LocalDeckStore.Save(profile, catalog, out string error, path)) throw new InvalidOperationException(error);
        LocalPlayerProfile loaded = LocalDeckStore.Load(catalog, out warning, path);
        if (warning != null || loaded.localPlayerId != profile.localPlayerId || loaded.guestName != profile.guestName ||
            loaded.decks.Count != profile.decks.Count || loaded.recentMatches.Count != 1 || loaded.settings.masterVolume != 0.6f)
            throw new InvalidOperationException("Local profile, decks, settings or history did not round-trip.");
        if (!LocalDeckStore.Save(loaded, catalog, out error, path) || !File.Exists(path + ".backup"))
            throw new InvalidOperationException("Local replacement save/backup failed: " + error);
        File.WriteAllText(path, "{broken json");
        LocalDeckStore.Load(catalog, out warning, path);
        if (string.IsNullOrEmpty(warning) || File.ReadAllText(path) != "{broken json")
            throw new InvalidOperationException("Corrupt profile protection failed.");
    }

    private static void Build()
    {
        CheckReady();
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        Write("building", "Building offline APK (IL2CPP, ARMv7 + ARM64). Unity Editor remains open.");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = Scenes, target = BuildTarget.Android, locationPathName = Output, options = BuildOptions.None });
        var errors = report.steps.SelectMany(step => step.messages)
            .Where(message => message.type == LogType.Error || message.type == LogType.Exception)
            .Select(message => message.content).Take(12).ToArray();
        string result = report.summary.result + "; " + report.summary.totalErrors + " errors; " +
            report.summary.totalWarnings + " warnings; " + report.summary.totalSize + " bytes.";
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Output), "BuildResult.txt"), result + "\n" + string.Join("\n", errors));
        if (report.summary.result != BuildResult.Succeeded || !File.Exists(Output))
            throw new InvalidOperationException(result + "\n" + string.Join("\n", errors));
        Write("succeeded", result);
        Debug.Log("Offline demo APK ready: " + Path.GetFullPath(Output));
    }

    private static void Write(string state, string message)
    {
        File.WriteAllText(Status, JsonUtility.ToJson(new Progress { state = state, message = message,
            output = Path.GetFullPath(Output), utc = DateTime.UtcNow.ToString("o") }, true));
    }
}
