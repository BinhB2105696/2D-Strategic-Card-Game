using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds only a transient Play-mode component. It never edits or saves the scene.
[InitializeOnLoad]
public static class DuelBoardReadOnlyDemoBootstrap
{
    private const string BoardBackgroundPath =
        "Assets/Art/UI/Board/Background.png";
    private const string PlayerOneAvatarPath =
        "Assets/Art/UI/Avatars/Avt2_TheVagrant.png";
    private const string PlayerTwoAvatarPath =
        "Assets/Art/UI/Avatars/Avt1_TheVagabond.png";
    private const string CardBackPath =
        "Assets/Art/Cards/Backs/Card_Back_Default.png";

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

        DuelBoardReadOnlyDemo demo = canvas.GetComponent<DuelBoardReadOnlyDemo>();
        if (demo == null)
            demo = canvas.AddComponent<DuelBoardReadOnlyDemo>();
        if (!TryConfigureDemo(demo))
            EditorApplication.isPlaying = false;
    }

    // Also used by the App Shell editor repair. It saves only serialized
    // references; no gameplay state or visual layout is changed here.
    internal static bool TryConfigureDemo(DuelBoardReadOnlyDemo demo)
    {
        GameObject monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Card_Monster.prefab");
        GameObject spellTrapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Card_SpellTrap.prefab");
        Sprite boardBackground = AssetDatabase.LoadAssetAtPath<Sprite>(
            BoardBackgroundPath);
        Sprite playerOneAvatar = AssetDatabase.LoadAssetAtPath<Sprite>(
            PlayerOneAvatarPath);
        Sprite playerTwoAvatar = AssetDatabase.LoadAssetAtPath<Sprite>(
            PlayerTwoAvatarPath);
        Sprite cardBack = AssetDatabase.LoadAssetAtPath<Sprite>(CardBackPath);
        if (monsterPrefab == null || spellTrapPrefab == null ||
            boardBackground == null || playerOneAvatar == null ||
            playerTwoAvatar == null || cardBack == null)
        {
            Debug.LogError("Playable demo is missing a card prefab or " +
                "board/Avatar/Card Back sprite. PvE scene was not configured.");
            return false;
        }
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
                return false;
            }

            cards.Add(card);
        }

        demo.Configure(
            cards.ToArray(),
            monsterPrefab,
            spellTrapPrefab,
            boardBackground,
            playerOneAvatar,
            playerTwoAvatar,
            cardBack);
        return true;
    }

    private static void EnsurePlayableDeckInEditMode()
    {
        if (isPreparingDeck ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        isPreparingDeck = true;
        try
        {
            if (!IsPlayableDeckReady())
            {
                Debug.LogError(
                    "The demo deck has missing cards or effects. Restore or edit the " +
                    "authored card assets; automatic sample configuration has been removed.");
            }
            else
            {
                CardSampleTool.AssignMonsterArtwork();
            }
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
