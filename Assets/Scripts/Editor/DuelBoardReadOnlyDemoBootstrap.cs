using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds only a transient Play-mode component. It never edits or saves the scene.
[InitializeOnLoad]
public static class DuelBoardReadOnlyDemoBootstrap
{
    static DuelBoardReadOnlyDemoBootstrap()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode ||
            SceneManager.GetActiveScene().name != "SampleScene")
            return;

        GameObject canvas = GameObject.Find("Field_Canvas");
        if (canvas == null)
            return;

        GameObject monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Card_Monster.prefab");
        GameObject spellTrapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Card_SpellTrap.prefab");
        CardData[] cards =
        {
            AssetDatabase.LoadAssetAtPath<CardData>(
                "Assets/Data/Cards/Monsters/Fire_Dragon.asset"),
            AssetDatabase.LoadAssetAtPath<CardData>(
                "Assets/Data/Cards/Monsters/Sample_Stone_Guardian.asset"),
            AssetDatabase.LoadAssetAtPath<CardData>(
                "Assets/Data/Cards/Spells/Sample_Arcane_Insight.asset"),
            AssetDatabase.LoadAssetAtPath<CardData>(
                "Assets/Data/Cards/Traps/Sample_Ambush.asset")
        };

        DuelBoardReadOnlyDemo demo = canvas.GetComponent<DuelBoardReadOnlyDemo>();
        if (demo == null)
            demo = canvas.AddComponent<DuelBoardReadOnlyDemo>();
        demo.Configure(cards, monsterPrefab, spellTrapPrefab);
    }
}
