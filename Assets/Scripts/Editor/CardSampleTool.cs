using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CardSampleTool
{
    private const string CardsRoot = "Assets/Data/Cards";
    private const string MonsterArtworkFolder =
        "Assets/Art/Cards/Monsters";

    [MenuItem("Card Game/Sample Cards/Assign Monster Artwork")]
    public static void AssignMonsterArtwork()
    {
        int assignedCount = 0;
        int missingCount = 0;
        AssignArtwork(
            "sample_stone_guardian",
            MonsterArtworkFolder + "/Stone_Guadian.png",
            ref assignedCount,
            ref missingCount);
        AssignArtwork(
            "sample_tide_sprite",
            MonsterArtworkFolder + "/Tide_Sprite.png",
            ref assignedCount,
            ref missingCount);
        AssignArtwork(
            "dawnwing_fairy",
            MonsterArtworkFolder + "/Dawnwing_Fairy.png",
            ref assignedCount,
            ref missingCount);
        AssignArtwork(
            "gravebound_imp",
            MonsterArtworkFolder + "/Gravebound Imp.png",
            ref assignedCount,
            ref missingCount);
        AssignArtwork(
            "fire_dragon",
            MonsterArtworkFolder + "/Fire_Dragon.png",
            ref assignedCount,
            ref missingCount);
        AssignArtwork(
            "sample_sky_sage",
            MonsterArtworkFolder + "/Sky Sage.png",
            ref assignedCount,
            ref missingCount);
        AssignArtwork(
            "legendary_fighter",
            MonsterArtworkFolder + "/Legendary Fighter.png",
            ref assignedCount,
            ref missingCount);

        if (assignedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log(
                "Assigned artwork to " + assignedCount +
                " Monster CardData asset(s). Missing pairs: " +
                missingCount + ".");
        }
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

            if (card.Effects.Count == 0)
            {
                Debug.LogError(
                    $"Playable sample card has no executable effect: {path}",
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

    private static void AssignArtwork(
        string cardId,
        string artworkPath,
        ref int assignedCount,
        ref int missingCount)
    {
        CardData card = FindCardById(cardId);
        Sprite artwork = AssetDatabase.LoadAssetAtPath<Sprite>(artworkPath);
        if (card == null || artwork == null)
        {
            missingCount++;
            Debug.LogWarning(
                "Cannot assign Monster artwork: card '" + cardId +
                "' or Sprite '" + artworkPath + "' is missing.");
            return;
        }

        if (card.Artwork == artwork)
            return;

        SerializedObject serializedCard = new SerializedObject(card);
        SerializedProperty artworkProperty =
            serializedCard.FindProperty("artwork");
        artworkProperty.objectReferenceValue = artwork;
        serializedCard.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(card);
        assignedCount++;
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

        if (effect.Kind == EffectKind.Activated &&
            effect.Trigger != EffectTriggerType.EndPhase &&
            effect.Resolution.Count == 0)
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

}
