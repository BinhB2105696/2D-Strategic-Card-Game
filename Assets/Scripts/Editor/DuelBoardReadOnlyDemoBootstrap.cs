using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds only a transient Play-mode component. It never edits or saves the scene.
[InitializeOnLoad]
public static class DuelBoardReadOnlyDemoBootstrap
{
    private static readonly string[] CardPaths =
    {
        "Assets/Data/Cards/Monsters/Sample_Stone_Guardian.asset",
        "Assets/Data/Cards/Monsters/Sample_Stone_Guardian.asset",
        "Assets/Data/Cards/Monsters/Sample_Stone_Guardian.asset",
        "Assets/Data/Cards/Monsters/Sample_Tide_Sprite.asset",
        "Assets/Data/Cards/Monsters/Sample_Tide_Sprite.asset",
        "Assets/Data/Cards/Monsters/Sample_Tide_Sprite.asset",
        "Assets/Data/Cards/Monsters/Dawnwing_Fairy.asset",
        "Assets/Data/Cards/Monsters/Dawnwing_Fairy.asset",
        "Assets/Data/Cards/Monsters/Dawnwing_Fairy.asset",
        "Assets/Data/Cards/Monsters/Gravebound_Imp.asset",
        "Assets/Data/Cards/Monsters/Gravebound_Imp.asset",
        "Assets/Data/Cards/Monsters/Gravebound_Imp.asset",
        "Assets/Data/Cards/Monsters/Fire_Dragon.asset",
        "Assets/Data/Cards/Monsters/Fire_Dragon.asset",
        "Assets/Data/Cards/Monsters/Sample_Sky_Sage.asset",
        "Assets/Data/Cards/Monsters/Legendary_Fighter.asset",
        "Assets/Data/Cards/Spells/Sample_Arcane_Insight.asset",
        "Assets/Data/Cards/Spells/Sample_Arcane_Insight.asset",
        "Assets/Data/Cards/Spells/Sample_Quick_Strike.asset",
        "Assets/Data/Cards/Spells/Sample_Quick_Strike.asset",
        "Assets/Data/Cards/Spells/Sample_Iron_Blade.asset",
        "Assets/Data/Cards/Spells/Test_Field_Spell.asset",
        "Assets/Data/Cards/Spells/Energy_Venom.asset",
        "Assets/Data/Cards/Spells/Power_Transfer.asset",
        "Assets/Data/Cards/Spells/Power_Transfer.asset",
        "Assets/Data/Cards/Spells/Temporal_Lock.asset",
        "Assets/Data/Cards/Traps/Sample_Ambush.asset",
        "Assets/Data/Cards/Traps/Sample_Ambush.asset",
        "Assets/Data/Cards/Traps/Sample_Divine_Reversal.asset",
        "Assets/Data/Cards/Traps/Guardian_Shift.asset"
    };

    private static bool isPreparingDeck;

    static DuelBoardReadOnlyDemoBootstrap()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.delayCall += EnsurePlayableDeckInEditMode;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += EnsurePlayableDeckInEditMode;
            return;
        }

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
        List<CardData> cards = new List<CardData>(30);
        foreach (string cardPath in CardPaths)
        {
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(cardPath);
            if (card == null || card.Effects.Count == 0)
            {
                Debug.LogError(
                    "Playable demo deck is incomplete at '" + cardPath +
                    "'. In Edit Mode run Card Game > Sample Cards > " +
                    "Prepare Fixed Playable Demo Deck, then enter Play again.");
                EditorApplication.isPlaying = false;
                return;
            }

            cards.Add(card);
        }

        DuelBoardReadOnlyDemo demo = canvas.GetComponent<DuelBoardReadOnlyDemo>();
        if (demo == null)
            demo = canvas.AddComponent<DuelBoardReadOnlyDemo>();
        demo.Configure(cards.ToArray(), monsterPrefab, spellTrapPrefab);
    }

    private static void EnsurePlayableDeckInEditMode()
    {
        if (isPreparingDeck ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            IsPlayableDeckReady())
        {
            return;
        }

        isPreparingDeck = true;
        try
        {
            CardSampleTool.PrepareFixedPlayableDemoDeck();
            Debug.Log(
                "The fixed playable demo deck was prepared automatically. " +
                "Play Mode is ready.");
        }
        finally
        {
            isPreparingDeck = false;
        }
    }

    private static bool IsPlayableDeckReady()
    {
        foreach (string cardPath in CardPaths)
        {
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(cardPath);
            if (card == null || card.Effects.Count == 0)
                return false;
        }
        return true;
    }
}
