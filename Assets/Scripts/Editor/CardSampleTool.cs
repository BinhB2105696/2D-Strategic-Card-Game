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
    private const string EffectsRoot = "Assets/Data/Effects";
    private const string SpellEffectsFolder = EffectsRoot + "/Spells";
    private const string ArcaneInsightEffectPath =
        SpellEffectsFolder + "/Arcane_Insight_Draw.asset";

    [MenuItem("Card Game/Sample Cards/Create Missing Sample Cards")]
    public static void CreateMissingSampleCards()
    {
        EnsureFolder(CardsRoot);
        EnsureFolder(MonstersFolder);
        EnsureFolder(SpellsFolder);
        EnsureFolder(TrapsFolder);
        EnsureFolder(EffectsRoot);
        EnsureFolder(SpellEffectsFolder);

        EffectData arcaneInsightEffect =
            CreateOrUpdateArcaneInsightEffect();

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
                    "A reliable Basic Monster used to test card data.");
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
                    "A Supreme Monster used to test Rank and tribute data.");
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
            SpellsFolder + "/Sample_Arcane_Insight.asset",
            "sample_arcane_insight",
            card =>
            {
                SetCommonData(
                    card,
                    "sample_arcane_insight",
                    "Arcane Insight",
                    CardCategory.Spell,
                    "Pay 1 Energy; draw 1 card.");
                SetEnum(card, "spellType", SpellType.Normal);
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
                    "A Normal Spell used to test Spell card presentation.");
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
                    "An Equip Spell used to test Spell subtype presentation.");
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
                    "A Normal Trap used to verify Speed 2 presentation.");
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
                    "A Counter Trap used to verify Speed 3 presentation.");
                SetEnum(card, "trapType", TrapType.Counter);
            },
            ref createdCount,
            ref skippedCount);

        ConfigureArcaneInsightCard(arcaneInsightEffect);

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

            for (int effectIndex = 0;
                 effectIndex < card.Effects.Count;
                 effectIndex++)
            {
                EffectData effect = card.Effects[effectIndex];

                if (effect == null)
                {
                    Debug.LogError(
                        $"CardData has a missing EffectData reference at index " +
                        $"{effectIndex}: {path}",
                        card);
                    errorCount++;
                }
            }
        }

        ValidateAllEffectData(ref errorCount);

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
        string effectText)
    {
        SetString(card, "cardId", cardId);
        SetString(card, "cardName", cardName);
        SetEnum(card, "category", category);
        SetString(card, "effectText", effectText);
    }

    private static EffectData CreateOrUpdateArcaneInsightEffect()
    {
        EffectData effect = AssetDatabase.LoadAssetAtPath<EffectData>(
            ArcaneInsightEffectPath);
        bool isNew = effect == null;

        if (isNew)
        {
            effect = ScriptableObject.CreateInstance<EffectData>();
        }

        SerializedObject serializedEffect = new SerializedObject(effect);
        SetString(
            serializedEffect,
            "effectId",
            "effect_arcane_insight_draw");
        SetEnum(
            serializedEffect,
            "trigger",
            EffectTriggerType.ManualActivation);
        SetEnum(serializedEffect, "activationPhase", TurnPhase.Main);
        SetInt(serializedEffect, "maxUsesPerTurn", 0);
        serializedEffect.FindProperty("conditions").arraySize = 0;

        SerializedProperty costs = serializedEffect.FindProperty("costs");
        costs.arraySize = 1;
        SerializedProperty cost = costs.GetArrayElementAtIndex(0);
        cost.FindPropertyRelative("type").enumValueIndex =
            (int)EffectCostType.AvailableEnergy;
        cost.FindPropertyRelative("amount").intValue = 1;

        SetEnum(serializedEffect, "targetType", EffectTargetType.None);
        SerializedProperty operations =
            serializedEffect.FindProperty("operations");
        operations.arraySize = 1;
        SerializedProperty operation = operations.GetArrayElementAtIndex(0);
        operation.FindPropertyRelative("type").enumValueIndex =
            (int)EffectOperationType.DrawCards;
        operation.FindPropertyRelative("amount").intValue = 1;
        serializedEffect.ApplyModifiedPropertiesWithoutUndo();

        if (isNew)
        {
            AssetDatabase.CreateAsset(effect, ArcaneInsightEffectPath);
        }

        EditorUtility.SetDirty(effect);
        return effect;
    }

    private static void ConfigureArcaneInsightCard(EffectData effect)
    {
        CardData card = FindCardById("sample_arcane_insight");

        if (card == null)
        {
            return;
        }

        SerializedObject serializedCard = new SerializedObject(card);
        SetString(
            serializedCard,
            "effectText",
            "Pay 1 Energy; draw 1 card.");
        SerializedProperty effects = serializedCard.FindProperty("effects");
        effects.arraySize = 1;
        effects.GetArrayElementAtIndex(0).objectReferenceValue = effect;
        serializedCard.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(card);
    }

    private static void ValidateAllEffectData(ref int errorCount)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:EffectData",
            new[] { EffectsRoot });
        Dictionary<string, string> pathsById =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EffectData effect = AssetDatabase.LoadAssetAtPath<EffectData>(path);

            if (effect == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(effect.EffectId))
            {
                Debug.LogError($"EffectData has an empty Effect ID: {path}", effect);
                errorCount++;
            }
            else if (pathsById.TryGetValue(effect.EffectId, out string firstPath))
            {
                Debug.LogError(
                    $"Duplicate Effect ID '{effect.EffectId}' found in:\n" +
                    $"- {firstPath}\n- {path}",
                    effect);
                errorCount++;
            }
            else
            {
                pathsById.Add(effect.EffectId, path);
            }

            if (effect.Operations.Count == 0)
            {
                Debug.LogError($"EffectData has no operations: {path}", effect);
                errorCount++;
            }

            for (int index = 0; index < effect.Operations.Count; index++)
            {
                EffectOperationData operation = effect.Operations[index];

                if (operation == null ||
                    operation.Type == EffectOperationType.None ||
                    operation.Amount <= 0)
                {
                    Debug.LogError(
                        $"EffectData has an invalid operation at index " +
                        $"{index}: {path}",
                        effect);
                    errorCount++;
                }
            }
        }
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
