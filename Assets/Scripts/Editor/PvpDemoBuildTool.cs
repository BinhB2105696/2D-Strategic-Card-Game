using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.SceneManagement;

// An explicit menu/file request builds the existing saved scenes without rebuilding their UI.
[InitializeOnLoad]
public static class PvpDemoBuildTool
{
    private const string Stage = "CardGame.PvpDemo.BuildStage";
    private const string RequestFile = "build-0.3.0.request";
    private const string Output = "Build/PvpDemo/0.3.0/CardGame-PvpDemo-0.3.0.apk";
    private static readonly string Control = Path.GetFullPath("Tools/Simulation/Control/PvpBuild");
    private static readonly string[] Scenes = { "Assets/Scenes/AppShell.unity", "Assets/Scenes/SampleScene.unity" };
    private static double stableSince, nextPoll;

    [Serializable] private sealed class Progress
    {
        public string state, message, output, utc;
        public int errors, warnings;
        public long bytes;
    }

    static PvpDemoBuildTool() { EditorApplication.update += Tick; }

    [MenuItem("Card Game/App Shell/Build PvP Demo APK")]
    public static void Request()
    {
        Directory.CreateDirectory(Control);
        File.WriteAllText(Path.Combine(Control, RequestFile), Path.GetFullPath("."));
    }

    [DidReloadScripts]
    private static void Reloaded()
    {
        if (SessionState.GetString(Stage, "") == "switching" &&
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            SessionState.SetString(Stage, "ready");
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { stableSince = 0; return; }
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1;
        try
        {
            string stage = SessionState.GetString(Stage, "");
            if (stage == "switching")
            {
                if (EditorUtility.scriptCompilationFailed)
                    throw new InvalidOperationException("Android script compilation failed. Resolve Console errors first.");
                return; // Android's assembly reload explicitly unlocks the build.
            }
            if (stage == "ready")
            {
                if (stableSince == 0) stableSince = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - stableSince < 5) return;
                CheckReady();
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    throw new InvalidOperationException("Android must remain the active build target.");
                SessionState.SetString(Stage, "");
                File.Delete(Path.Combine(Control, RequestFile));
                Build();
                return;
            }
            string request = Path.Combine(Control, RequestFile);
            if (!File.Exists(request)) return;
            if (!string.Equals(File.ReadAllText(request).Trim(), Path.GetFullPath("."), StringComparison.OrdinalIgnoreCase)) return;
            CheckReady();
            PvpPacketVerification.Run();
            PvpMatchVerification.Verify();
            PvpOfficialUiVerification.Run();
            Configure();
            stableSince = 0;
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                SessionState.SetString(Stage, "ready");
            else
            {
                SessionState.SetString(Stage, "switching");
                Write("switching", "Switching the existing Editor to Android and waiting for script reload.");
                if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android))
                    throw new InvalidOperationException("Could not switch to Android.");
            }
        }
        catch (Exception exception)
        {
            SessionState.SetString(Stage, "");
            File.Delete(Path.Combine(Control, RequestFile));
            Write("blocked", exception.Message);
            Debug.LogException(exception);
        }
    }

    private static void CheckReady()
    {
        if (Application.unityVersion != "2022.3.62f3")
            throw new InvalidOperationException("This build must use Unity 2022.3.62f3.");
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before building.");
        if (EditorUtility.scriptCompilationFailed)
            throw new InvalidOperationException("Fix Console compilation errors before building.");
        for (int index = 0; index < SceneManager.sceneCount; index++)
            if (SceneManager.GetSceneAt(index).isDirty)
                throw new InvalidOperationException("Save the scene before building: " + SceneManager.GetSceneAt(index).path);
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new InvalidOperationException("Android Build Support is missing; do not upgrade Unity.");
        foreach (string scene in Scenes)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                throw new InvalidOperationException("Missing build scene: " + scene);
        if (string.IsNullOrEmpty(Application.cloudProjectId))
            throw new InvalidOperationException("The project must stay linked to its existing Unity Cloud project.");
    }

    private static void Configure()
    {
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.binhb2105696.strategiccardgame.demo");
        PlayerSettings.bundleVersion = "0.3.0";
        PlayerSettings.Android.bundleVersionCode = 4;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)23;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
        PlayerSettings.Android.forceInternetPermission = true;
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        // Existing company/product identity and signing settings are deliberately preserved.
        Write("configured", "PvP APK 0.3.0/code 4; official duel UI, turn clock and disconnect forfeits.");
    }

    private static void Build()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        Write("building", "Building PvP APK. The Unity Editor remains open.");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        { scenes = Scenes, target = BuildTarget.Android, locationPathName = Output, options = BuildOptions.None });
        string[] issues = report.steps.SelectMany(step => step.messages)
            .Where(message => message.type == LogType.Error || message.type == LogType.Exception)
            .Select(message => message.content).Take(12).ToArray();
        string summary = report.summary.result + "; " + report.summary.totalErrors + " errors; " +
            report.summary.totalWarnings + " warnings; " + report.summary.totalSize + " bytes.";
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Output), "BuildResult.txt"), summary + "\n" + string.Join("\n", issues));
        if (report.summary.result != BuildResult.Succeeded || !File.Exists(Output))
            throw new InvalidOperationException(summary + "\n" + string.Join("\n", issues));
        Write("succeeded", summary, (int)report.summary.totalErrors, (int)report.summary.totalWarnings, (long)report.summary.totalSize);
        Debug.Log("PvP demo APK ready: " + Path.GetFullPath(Output));
    }

    private static void Write(string state, string message, int errors = 0, int warnings = 0, long bytes = 0)
    {
        Directory.CreateDirectory(Control);
        File.WriteAllText(Path.Combine(Control, "status.json"), JsonUtility.ToJson(new Progress
        { state = state, message = message, output = Path.GetFullPath(Output), utc = DateTime.UtcNow.ToString("o"),
            errors = errors, warnings = warnings, bytes = bytes }, true));
    }
}
