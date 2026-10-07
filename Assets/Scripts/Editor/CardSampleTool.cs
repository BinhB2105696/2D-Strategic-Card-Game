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
    private const string ComponentsRoot = "Assets/Data/EffectComponents";
    private const string ConditionFolder = ComponentsRoot + "/Conditions";
    private const string CostFolder = ComponentsRoot + "/Costs";
    private const string TargetFolder = ComponentsRoot + "/Targets";
    private const string ResolutionFolder = ComponentsRoot + "/Resolutions";
    private const string ModifierFolder = ComponentsRoot + "/ContinuousModifiers";
    private const string PayEnergyPath = CostFolder + "/Pay_Energy.asset";
    private const string DrawCardsPath = ResolutionFolder + "/Draw_Cards.asset";

    [MenuItem("Card Game/Sample Cards/Create Missing Sample Cards")]
    public static void CreateMissingSampleCards()
    {
        EnsureFolder(CardsRoot);
        EnsureFolder(MonstersFolder);
        EnsureFolder(SpellsFolder);
        EnsureFolder(TrapsFolder);
        EnsureFolder(ComponentsRoot);
        EnsureFolder(ConditionFolder);
        EnsureFolder(CostFolder);
        EnsureFolder(TargetFolder);
        EnsureFolder(ResolutionFolder);
        EnsureFolder(ModifierFolder);
        PayEnergyCostAsset payEnergy = CreateComponent<PayEnergyCostAsset>(
            PayEnergyPath);
        DrawCardsResolutionAsset drawCards =
            CreateComponent<DrawCardsResolutionAsset>(DrawCardsPath);

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

        ConfigureArcaneInsightCard(payEnergy, drawCards);

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

            HashSet<string> effectIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int effectIndex = 0;
                 effectIndex < card.Effects.Count;
                 effectIndex++)
            {
                CardEffectData effect = card.Effects[effectIndex];

                if (effect == null)
                {
                    Debug.LogError(
                        $"CardData has an empty effect at index " +
                        $"{effectIndex}: {path}",
                        card);
                    errorCount++;
                    continue;
                }

                ValidateEffect(card, effect, effectIndex, path, ref errorCount);
                if (!string.IsNullOrWhiteSpace(effect.EffectId) &&
                    !effectIds.Add(effect.EffectId))
                {
                    Debug.LogError(
                        $"Duplicate Effect ID '{effect.EffectId}' on card: {path}",
                        card);
                    errorCount++;
                }
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
        string effectText)
    {
        SetString(card, "cardId", cardId);
        SetString(card, "cardName", cardName);
        SetEnum(card, "category", category);
        SetString(card, "effectText", effectText);
    }

    private static T CreateComponent<T>(string path)
        where T : ScriptableObject
    {
        T component = AssetDatabase.LoadAssetAtPath<T>(path);
        if (component != null)
            return component;

        component = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(component, path);
        return component;
    }

    private static void ConfigureArcaneInsightCard(
        PayEnergyCostAsset payEnergy,
        DrawCardsResolutionAsset drawCards)
    {
        CardData card = FindCardById("sample_arcane_insight");

        if (card == null)
        {
            return;
        }

        SerializedObject serializedCard = new SerializedObject(card);
        SerializedProperty effects = serializedCard.FindProperty("effects");
        if (effects.arraySize != 0)
            return;

        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("effectId").stringValue =
            "effect_arcane_insight_draw";
        effect.FindPropertyRelative("effectKind").enumValueIndex =
            (int)EffectKind.Activated;
        effect.FindPropertyRelative("trigger").enumValueIndex =
            (int)EffectTriggerType.ManualActivation;
        effect.FindPropertyRelative("allowedPhases").intValue =
            (int)EffectPhaseMask.Main;
        effect.FindPropertyRelative("allowedTurn").enumValueIndex =
            (int)EffectTurnScope.YourTurn;
        effect.FindPropertyRelative("activationWindow").enumValueIndex =
            (int)EffectActivationWindow.OpenGameState;
        SerializedProperty costs = effect.FindPropertyRelative("costs");
        costs.arraySize = 1;
        SerializedProperty cost = costs.GetArrayElementAtIndex(0);
        cost.FindPropertyRelative("component").objectReferenceValue = payEnergy;
        cost.FindPropertyRelative("amount").intValue = 1;
        SerializedProperty resolution =
            effect.FindPropertyRelative("resolution");
        resolution.arraySize = 1;
        SerializedProperty operation = resolution.GetArrayElementAtIndex(0);
        operation.FindPropertyRelative("component").objectReferenceValue =
            drawCards;
        operation.FindPropertyRelative("amount").intValue = 1;
        serializedCard.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(card);
    }

    private static void ValidateEffect(
        CardData card,
        CardEffectData effect,
        int index,
        string path,
        ref int errorCount)
    {
        int componentErrors = 0;
        if (string.IsNullOrWhiteSpace(effect.EffectId))
        {
            Debug.LogError($"Effect {index} needs an Effect ID: {path}", card);
            errorCount++;
        }

        if (effect.Kind == EffectKind.Activated && effect.Resolution.Count == 0)
        {
            Debug.LogError($"Activated effect {index} has no resolution: {path}", card);
            errorCount++;
        }

        if (effect.Kind == EffectKind.Continuous &&
            effect.ContinuousModifiers.Count == 0)
        {
            Debug.LogError($"Continuous effect {index} has no modifier: {path}", card);
            errorCount++;
        }

        if (effect.Targets.Count > 1)
        {
            Debug.LogError(
                $"Effect {index} has multiple targets, which are not supported yet: {path}",
                card);
            errorCount++;
        }

        for (int i = 0; i < effect.Conditions.Count; i++)
            if (effect.Conditions[i] == null || effect.Conditions[i].Component == null)
                ReportMissing("condition", i);
        for (int i = 0; i < effect.Costs.Count; i++)
            if (effect.Costs[i] == null || effect.Costs[i].Component == null ||
                effect.Costs[i].Amount <= 0)
                ReportMissing("cost", i);
        for (int i = 0; i < effect.Targets.Count; i++)
            if (effect.Targets[i] == null || effect.Targets[i].Component == null ||
                effect.Targets[i].Amount <= 0)
                ReportMissing("target", i);
        for (int i = 0; i < effect.Resolution.Count; i++)
        {
            EffectResolutionSlot resolution = effect.Resolution[i];
            if (resolution == null || resolution.Component == null ||
                resolution.Amount <= 0)
            {
                ReportMissing("resolution", i);
                continue;
            }

            try
            {
                resolution.Component.CreateDefinition(resolution.Amount);
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    $"Effect {index} has an invalid resolution at {i}: " +
                    $"{path}. {exception.Message}", card);
                componentErrors++;
            }
        }
        for (int i = 0; i < effect.ContinuousModifiers.Count; i++)
            if (effect.ContinuousModifiers[i] == null ||
                effect.ContinuousModifiers[i].Component == null)
                ReportMissing("modifier", i);

        void ReportMissing(string componentKind, int componentIndex)
        {
            Debug.LogError(
                $"Effect {index} has an invalid {componentKind} at " +
                $"{componentIndex}: {path}. Assign a component and valid " +
                "Amount, or remove the unused list element.", card);
            componentErrors++;
        }

        errorCount += componentErrors;
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
