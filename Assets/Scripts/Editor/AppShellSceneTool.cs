using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Creates a separate scene with Unity APIs. It never writes Unity YAML by hand.
[InitializeOnLoad]
public static class AppShellSceneTool
{
    private const string AppScenePath = "Assets/Scenes/AppShell.unity";
    private const string DuelScenePath = "Assets/Scenes/SampleScene.unity";
    private const string CardRoot = "Assets/Data/Cards";

    static AppShellSceneTool()
    {
        EditorApplication.delayCall += CreateIfMissing;
        EditorApplication.delayCall += RepairScenesIfSafe;
        EditorApplication.delayCall += PrepareEditableUi;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += RepairScenesIfSafe;
                EditorApplication.delayCall += PrepareEditableUi;
            }
            else
            {
                EditorApplication.delayCall -= RepairScenesIfSafe;
                EditorApplication.delayCall -= PrepareEditableUi;
            }
        };
    }

    [MenuItem("Card Game/App Shell/Prepare Editable UI")]
    public static void PrepareEditableUi()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(AppScenePath)) return;
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(AppScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(AppScenePath, OpenSceneMode.Additive);
        try
        {
            DeckBuilderAppController controller = scene.GetRootGameObjects()
                .Select(root => root.GetComponent<DeckBuilderAppController>()).FirstOrDefault(c => c != null);
            if (controller == null || controller.HasAuthoredUi) return;
            if (scene.isDirty)
            {
                Debug.LogWarning("Save AppShell in Edit Mode, then run Card Game > App Shell > Prepare Editable UI. Existing edits were preserved.");
                return;
            }
            SceneManager.SetActiveScene(scene);
            AssignReferences(controller);
            SerializedObject serialized = new SerializedObject(controller);
            SerializedProperty avatars = serialized.FindProperty("avatarSprites");
            string[] paths = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/UI/Avatars" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
            avatars.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++) avatars.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
            serialized.FindProperty("duelOverlayPrefab").objectReferenceValue = EnsureDuelOverlay();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            controller.PrepareEditableUi();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("AppShell editable UI prepared. Select AppShell in Hierarchy; use its Inspector preview buttons. Layouts will not be regenerated on reload.");
            EditorApplication.delayCall += AppFoundationVerification.Run;
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static GameObject EnsureDuelOverlay()
    {
        const string path = "Assets/Prefabs/AppDuelOverlay.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        GameObject root = new GameObject("AppDuelOverlay", typeof(RectTransform), typeof(AppDuelFlow));
        try
        {
            root.GetComponent<AppDuelFlow>().BuildEditorView(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Fonts/TMP/Cinzel-SemiBold SDF.asset"));
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [MenuItem("Card Game/App Shell/Create App Shell Scene If Missing")]
    public static void CreateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || File.Exists(AppScenePath))
            return;
        CreateScene();
    }

    [MenuItem("Card Game/App Shell/Refresh Card Catalog In App Shell")]
    public static void RefreshCatalog()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Refresh the app catalog in Edit Mode only.");
            return;
        }
        Scene scene = SceneManager.GetSceneByPath(AppScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(AppScenePath,
                OpenSceneMode.Additive);
        try
        {
            DeckBuilderAppController controller = scene.GetRootGameObjects()
                .Select(go => go.GetComponent<DeckBuilderAppController>())
                .FirstOrDefault(value => value != null);
            if (controller == null)
            {
                Debug.LogError("AppShell scene has no DeckBuilderAppController.");
                return;
            }
            AssignReferences(controller);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("App Shell card catalog refreshed from CardData assets.");
        }
        finally
        {
            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [MenuItem("Card Game/App Shell/Repair Camera and PvE Launch")]
    public static void RepairScenesIfSafe()
    {
        if (!CanEditScenesNow || !File.Exists(AppScenePath) ||
            !File.Exists(DuelScenePath))
            return;

        RepairAppShellCamera();
        ConnectDemoToSampleScene();
        if (CanEditScenesNow) EnsureBuildScenes();
    }

    private static bool CanEditScenesNow => !Application.isPlaying &&
        !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode &&
        !EditorApplication.isCompiling && !EditorApplication.isUpdating;

    private static void CreateScene()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene shell = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            NewSceneMode.Additive);
        try
        {
            GameObject root = new GameObject("AppShell");
            SceneManager.MoveGameObjectToScene(root, shell);
            DeckBuilderAppController controller =
                root.AddComponent<DeckBuilderAppController>();
            AssignReferences(controller);
            AddAppCamera(shell);
            EditorSceneManager.SaveScene(shell, AppScenePath);
            EnsureBuildScenes();
            Debug.Log("Created AppShell scene with guest entry, Main Menu, " +
                "and Deck Builder. Open Assets/Scenes/AppShell.unity to test.");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(shell, true);
        }
    }

    private static void RepairAppShellCamera()
    {
        if (!CanEditScenesNow) return;
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(AppScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(AppScenePath,
                OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty)
            {
                Debug.LogWarning("AppShell scene has unsaved changes. Save it " +
                    "in Edit Mode, then run Card Game > App Shell > " +
                    "Repair Camera and PvE Launch.");
                return;
            }

            bool hasCamera = scene.GetRootGameObjects().Any(root =>
                root.GetComponentsInChildren<Camera>(true).Any(camera =>
                    camera.enabled && camera.targetDisplay == 0));
            if (!hasCamera)
            {
                AddAppCamera(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("AppShell camera added; Game view will no longer " +
                    "show 'No cameras rendering'.");
            }
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AddAppCamera(Scene scene)
    {
        GameObject go = new GameObject("App Camera", typeof(Camera),
            typeof(AudioListener));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = go.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.02f, .04f, .09f, 1f);
        camera.orthographic = true;
        camera.cullingMask = 0; // The app UI uses Screen Space Overlay.
        camera.targetDisplay = 0;
    }

    private static void ConnectDemoToSampleScene()
    {
        if (!CanEditScenesNow) return;
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(DuelScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(DuelScenePath,
                OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty)
            {
                Debug.LogWarning("SampleScene has unsaved changes. Save it " +
                    "in Edit Mode, then run Card Game > App Shell > " +
                    "Repair Camera and PvE Launch. No duel layout was changed.");
                return;
            }

            GameObject canvas = scene.GetRootGameObjects().FirstOrDefault(
                root => root.name == "Field_Canvas");
            if (canvas == null)
            {
                Debug.LogError("SampleScene is missing Field_Canvas.");
                return;
            }

            DuelBoardReadOnlyDemo demo =
                canvas.GetComponent<DuelBoardReadOnlyDemo>();
            if (demo != null)
            {
                SerializedObject existing = new SerializedObject(demo);
                if (existing.FindProperty("sampleCards").arraySize == 30 &&
                    existing.FindProperty("monsterCardPrefab")
                        .objectReferenceValue != null &&
                    existing.FindProperty("spellTrapCardPrefab")
                        .objectReferenceValue != null)
                    return;
            }
            else
                demo = canvas.AddComponent<DuelBoardReadOnlyDemo>();

            if (!DuelBoardReadOnlyDemoBootstrap.TryConfigureDemo(demo))
            {
                Debug.LogError("SampleScene was not saved because the fixed " +
                    "playable deck is incomplete.");
                return;
            }
            EditorUtility.SetDirty(demo);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("SampleScene now has a serialized duel starter on " +
                "Field_Canvas. Its positions and visual hierarchy are unchanged.");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AssignReferences(DeckBuilderAppController controller)
    {
        string[] guids = AssetDatabase.FindAssets("t:CardData",
            new[] { CardRoot });
        List<(CardData card, long updated)> entries =
            new List<(CardData card, long updated)>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card != null)
                entries.Add((card, File.GetLastWriteTimeUtc(path).Ticks));
        }
        entries.Sort((a, b) => string.Compare(a.card.CardId,
            b.card.CardId, StringComparison.Ordinal));
        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty cards = serialized.FindProperty("catalogCards");
        SerializedProperty times = serialized.FindProperty("catalogUpdatedTicks");
        cards.arraySize = entries.Count;
        times.arraySize = entries.Count;
        for (int i = 0; i < entries.Count; i++)
        {
            cards.GetArrayElementAtIndex(i).objectReferenceValue = entries[i].card;
            times.GetArrayElementAtIndex(i).longValue = entries[i].updated;
        }
        serialized.FindProperty("monsterCardPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Card_Monster.prefab");
        serialized.FindProperty("spellTrapCardPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Card_SpellTrap.prefab");
        serialized.FindProperty("uiFont").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Fonts/TMP/Cinzel-SemiBold SDF.asset");
        serialized.FindProperty("backgroundSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Board/Background.png");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void EnsureBuildScenes()
    {
        List<EditorBuildSettingsScene> scenes =
            new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(scene => scene.path == AppScenePath))
            scenes.Insert(0, new EditorBuildSettingsScene(AppScenePath, true));
        if (!scenes.Exists(scene => scene.path == DuelScenePath))
            scenes.Add(new EditorBuildSettingsScene(DuelScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
