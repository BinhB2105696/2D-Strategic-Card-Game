using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CardSampleTool
{
    private const string CardsRoot = "Assets/Data/Cards";
    private const string MonstersFolder = CardsRoot + "/Monsters";
    private const string SpellsFolder = CardsRoot + "/Spells";
    private const string TrapsFolder = CardsRoot + "/Traps";

    [MenuItem("Card Game/Sample Cards/Create Missing Sample Cards")]
    public static void CreateMissingSampleCards()
    {
        EnsureFolder(CardsRoot);
        EnsureFolder(MonstersFolder);
        EnsureFolder(SpellsFolder);
        EnsureFolder(TrapsFolder);

        int createdCount = 0;
        int skippedCount = 0;

        CreateCardIfMissing(
            MonstersFolder + "/Sample_Stone_Guardian.asset",
            "sample_stone_guardian",
            card =>
            {
                SetCommonData(
                    card,
                    "sample_stone_guardian",
                    "Stone Guardian",
                    CardCategory.Monster,
                    "A reliable Basic Monster used to test card data.",
                    1);
                SetEnum(card, "monsterAttribute", MonsterAttribute.Earth);
                SetEnum(card, "monsterRank", MonsterRank.Basic);
                SetEnum(card, "monsterType", MonsterType.Spirit);
                SetInt(card, "baseHealth", 1200);
                SetInt(card, "baseAttack", 700);
                SetInt(card, "baseDefense", 1100);
                SetInt(card, "attackEnergyRequirement", 1);
            },
            ref createdCount,
            ref skippedCount);

        CreateCardIfMissing(
            MonstersFolder + "/Sample_Sky_Sage.asset",
            "sample_sky_sage",
            card =>
            {
                SetCommonData(
                    card,
                    "sample_sky_sage",
                    "Sky Sage",
                    CardCategory.Monster,
                    "A Supreme Monster used to test Rank and tribute data.",
                    2);
                SetEnum(card, "monsterAttribute", MonsterAttribute.Wind);
                SetEnum(card, "monsterRank", MonsterRank.Supreme);
                SetEnum(card, "monsterType", MonsterType.Spellcaster);
                SetInt(card, "baseHealth", 2600);
                SetInt(card, "baseAttack", 2200);
                SetInt(card, "baseDefense", 1800);
                SetInt(card, "attackEnergyRequirement", 3);
            },
            ref createdCount,
            ref skippedCount);

        CreateCardIfMissing(
            SpellsFolder + "/Sample_Quick_Strike.asset",
            "sample_quick_strike",
            card =>
            {
                SetCommonData(
                    card,
                    "sample_quick_strike",
                    "Quick Strike",
                    CardCategory.Spell,
                    "A Normal Spell used to test Spell card presentation.",
                    1);
                SetEnum(card, "spellType", SpellType.Normal);
            },
            ref createdCount,
            ref skippedCount);

        CreateCardIfMissing(
            SpellsFolder + "/Sample_Iron_Blade.asset",
            "sample_iron_blade",
            card =>
            {
                SetCommonData(
                    card,
                    "sample_iron_blade",
                    "Iron Blade",
                    CardCategory.Spell,
                    "An Equip Spell used to test Spell subtype presentation.",
                    2);
                SetEnum(card, "spellType", SpellType.Equip);
            },
            ref createdCount,
            ref skippedCount);

        CreateCardIfMissing(
            TrapsFolder + "/Sample_Ambush.asset",
            "sample_ambush",
            card =>
            {
                SetCommonData(
                    card,
                    "sample_ambush",
                    "Ambush",
                    CardCategory.Trap,
                    "A Normal Trap used to verify Speed 2 presentation.",
                    1);
                SetEnum(card, "trapType", TrapType.Normal);
            },
            ref createdCount,
            ref skippedCount);

        CreateCardIfMissing(
            TrapsFolder + "/Sample_Divine_Reversal.asset",
            "sample_divine_reversal",
            card =>
            {
                SetCommonData(
                    card,
                    "sample_divine_reversal",
                    "Divine Reversal",
                    CardCategory.Trap,
                    "A Counter Trap used to verify Speed 3 presentation.",
                    3);
                SetEnum(card, "trapType", TrapType.Counter);
            },
            ref createdCount,
            ref skippedCount);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Sample card generation finished. Created: {createdCount}, " +
            $"skipped existing: {skippedCount}. Run Validate All Card Data next.");
    }

    [MenuItem("Card Game/Sample Cards/Validate All Card Data")]
    public static void ValidateAllCardData()
    {
        string[] cardGuids = AssetDatabase.FindAssets(
            "t:CardData",
            new[] { CardsRoot });

        Dictionary<string, string> pathsById =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int errorCount = 0;
        int warningCount = 0;

        foreach (string guid in cardGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);

            if (card == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(card.CardId))
            {
                Debug.LogError($"CardData has an empty Card ID: {path}", card);
                errorCount++;
            }
            else if (pathsById.TryGetValue(card.CardId, out string firstPath))
            {
                Debug.LogError(
                    $"Duplicate Card ID '{card.CardId}' found in:\n" +
                    $"- {firstPath}\n- {path}",
                    card);
                errorCount++;
            }
            else
            {
                pathsById.Add(card.CardId, path);
            }

            if (string.IsNullOrWhiteSpace(card.CardName))
            {
                Debug.LogError($"CardData has an empty Card Name: {path}", card);
                errorCount++;
            }

            if (card.Artwork == null)
            {
                Debug.LogWarning($"CardData has no artwork yet: {path}", card);
                warningCount++;
            }

            if (card.Category == CardCategory.Monster && card.BaseHealth <= 0)
            {
                Debug.LogError(
                    $"Monster must have Base Health greater than 0: {path}",
                    card);
                errorCount++;
            }
        }

        Debug.Log(
            $"CardData validation finished. Cards: {cardGuids.Length}, " +
            $"errors: {errorCount}, warnings: {warningCount}.");
    }

    private static void CreateCardIfMissing(
        string assetPath,
        string cardId,
        Action<SerializedObject> configure,
        ref int createdCount,
        ref int skippedCount)
    {
        if (AssetDatabase.LoadAssetAtPath<CardData>(assetPath) != null ||
            FindCardById(cardId) != null)
        {
            skippedCount++;
            return;
        }

        CardData card = ScriptableObject.CreateInstance<CardData>();
        SerializedObject serializedCard = new SerializedObject(card);
        configure(serializedCard);
        serializedCard.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.CreateAsset(card, assetPath);
        EditorUtility.SetDirty(card);
        createdCount++;
    }

    private static CardData FindCardById(string cardId)
    {
        string[] cardGuids = AssetDatabase.FindAssets(
            "t:CardData",
            new[] { CardsRoot });

        foreach (string guid in cardGuids)
        {
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(
                AssetDatabase.GUIDToAssetPath(guid));

            if (card != null && string.Equals(
                    card.CardId,
                    cardId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return card;
            }
        }

        return null;
    }

    private static void SetCommonData(
        SerializedObject card,
        string cardId,
        string cardName,
        CardCategory category,
        string effectText,
        int energyCost)
    {
        SetString(card, "cardId", cardId);
        SetString(card, "cardName", cardName);
        SetEnum(card, "category", category);
        SetString(card, "effectText", effectText);
        SetInt(card, "energyCost", energyCost);
    }

    private static void SetString(
        SerializedObject target,
        string propertyName,
        string value)
    {
        target.FindProperty(propertyName).stringValue = value;
    }

    private static void SetInt(
        SerializedObject target,
        string propertyName,
        int value)
    {
        target.FindProperty(propertyName).intValue = value;
    }

    private static void SetEnum<T>(
        SerializedObject target,
        string propertyName,
        T value)
        where T : Enum
    {
        target.FindProperty(propertyName).enumValueIndex = Convert.ToInt32(value);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = folderPath.Substring(0, folderPath.LastIndexOf('/'));
        string folderName = folderPath.Substring(folderPath.LastIndexOf('/') + 1);

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
