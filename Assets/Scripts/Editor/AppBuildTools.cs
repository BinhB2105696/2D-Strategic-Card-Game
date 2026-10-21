using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AppBuildTools
{
    [MenuItem("Card Game/App Shell/Apply Portrait Build Defaults")]
    public static void ApplyPortraitDefaults()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.defaultScreenWidth = 540;
        PlayerSettings.defaultScreenHeight = 960;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        Debug.Log("App build defaults: Android portrait; Windows 540x960 resizable window. Company/product identity and save location unchanged.");
    }

    [MenuItem("Card Game/App Shell/Build Development Windows")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Build/AppShellWindows/CardGame.exe");

    [MenuItem("Card Game/App Shell/Build Development APK")]
    public static void BuildAndroid() => Build(BuildTarget.Android, "Build/AppShellAndroid/CardGame.apk");

    private static void Build(BuildTarget target, string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AppShellSceneTool.PrepareEditableUi();
        var shell = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/AppShell.unity");
        bool openedHere = !shell.IsValid() || !shell.isLoaded;
        if (openedHere) shell = EditorSceneManager.OpenScene("Assets/Scenes/AppShell.unity", OpenSceneMode.Additive);
        bool ready;
        try
        {
            ready = shell.GetRootGameObjects().Select(root => root.GetComponent<DeckBuilderAppController>())
                .Any(controller => controller != null && controller.HasAuthoredUi);
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(shell, true); }
        if (!ready)
        {
            Debug.LogError("AppShell has no saved editable UI. Prepare it before building.");
            return;
        }
        if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
        {
            Debug.LogError("Install the matching Unity build module using Unity Hub before building " + target);
            return;
        }
        ApplyPortraitDefaults();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/AppShell.unity", "Assets/Scenes/SampleScene.unity" },
            target = target, locationPathName = path, options = BuildOptions.Development
        });
        Debug.Log("App build " + target + ": " + report.summary.result + "; " + report.summary.totalErrors +
            " errors. Output: " + Path.GetFullPath(path));
    }
}
