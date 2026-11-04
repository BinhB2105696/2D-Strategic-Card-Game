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
    private const string MonsterArtworkFolder =
        "Assets/Art/Cards/Monsters";
    private const string ComponentsRoot = "Assets/Data/EffectComponents";
    private const string ConditionFolder = ComponentsRoot + "/Conditions";
    private const string CostFolder = ComponentsRoot + "/Costs";
    private const string TargetFolder = ComponentsRoot + "/Targets";
    private const string ResolutionFolder = ComponentsRoot + "/Resolutions";
    private const string ModifierFolder = ComponentsRoot + "/ContinuousModifiers";
    private const string PayEnergyPath = CostFolder + "/Pay_Energy.asset";
    private const string DrawCardsPath = ResolutionFolder + "/Draw_Cards.asset";
    private const string OwnMonsterPath = TargetFolder + "/Own_Monster.asset";
    private const string OpponentMonsterPath =
        TargetFolder + "/Opponent_Monster.asset";
    private const string ModifyAttackPath =
        ModifierFolder + "/Modify_Attack.asset";
    private const string ModifyDefensePath =
        ModifierFolder + "/Modify_Defense.asset";
    private const string ReduceAttackPath =
        ResolutionFolder + "/Reduce_Attack_Until_End_Of_Turn.asset";
    private const string NegatePreviousPath =
        ResolutionFolder + "/Negate_Previous_Activation.asset";
    private const string IncreaseAttackPath =
        ResolutionFolder + "/Increase_Attack_Until_End_Of_Turn.asset";
    private const string IncreaseDefensePath =
        ResolutionFolder + "/Increase_Defense_Until_End_Of_Turn.asset";
    private const string RestoreHealthPath =
        ResolutionFolder + "/Restore_Monster_Health.asset";
    private const string PermanentDefensePath =
        ResolutionFolder + "/Increase_Defense_Permanently.asset";
    private const string TriggerAttackPath =
        ResolutionFolder + "/Increase_Attack_By_Trigger_Value.asset";
    private const string EnergyHealthPath =
        ResolutionFolder + "/Restore_Health_Per_Removed_Energy.asset";
    private const string ProtectAttackedMonsterPath =
        ResolutionFolder + "/Protect_Attacked_Monster.asset";
    private const string TransferAttackPath =
        ResolutionFolder + "/Transfer_Attack_Until_End_Of_Turn.asset";
    private const string SkipDrawPath =
        ResolutionFolder + "/Skip_Next_Opponent_Draw.asset";
    private const string SkipBattlePath =
        ResolutionFolder + "/Skip_Current_Battle_Phase.asset";
    private const string SearchBasicPath =
        ResolutionFolder + "/Search_Basic_Monster_From_Deck.asset";
    private const string RecoverBasicPath =
        ResolutionFolder + "/Recover_Basic_Monster_From_Graveyard.asset";
    private const string ModifyAttackPerEnergyPath =
        ModifierFolder + "/Modify_Attack_Per_Target_Energy.asset";

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
            MonstersFolder + "/Sample_Tide_Sprite.asset",
            "sample_tide_sprite",
            card => ConfigureTideSpriteBaseData(card),
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
        ConfigurePlayableSpellTrapSamplesInternal();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Sample card generation finished. Created: {createdCount}, " +
            $"skipped existing: {skippedCount}. Run Validate All Card Data next.");
    }

    [MenuItem("Card Game/Sample Cards/Configure Playable Spell and Trap Samples")]
    public static void ConfigurePlayableSpellTrapSamples()
    {
        EnsureFolder(CardsRoot);
        EnsureFolder(MonstersFolder);
        EnsureFolder(SpellsFolder);
        EnsureFolder(TrapsFolder);
        EnsureFolder(ComponentsRoot);
        EnsureFolder(CostFolder);
        EnsureFolder(TargetFolder);
        EnsureFolder(ResolutionFolder);
        EnsureFolder(ModifierFolder);
        ConfigurePlayableSpellTrapSamplesInternal();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "Playable demo card configuration finished. The fixed 30-card " +
            "deck now favors Basic Monsters and keeps every card at 3 copies " +
            "or fewer.");
    }

    [MenuItem("Card Game/Sample Cards/Prepare Fixed Playable Demo Deck")]
    public static void PrepareFixedPlayableDemoDeck()
    {
        ConfigurePlayableSpellTrapSamples();
        AssignMonsterArtwork();
        ValidateAllCardData();
    }

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

    private static void SetString(
        SerializedObject target,
        string propertyName,
        string value)
    {
        target.FindProperty(propertyName).stringValue = value;
    }

    private static void ConfigurePlayableSpellTrapSamplesInternal()
    {
        EnsureRequestedCardsExist();
        PayEnergyCostAsset payEnergy = CreateComponent<PayEnergyCostAsset>(
            PayEnergyPath);
        OwnMonsterTargetAsset ownMonster =
            CreateComponent<OwnMonsterTargetAsset>(OwnMonsterPath);
        CreateComponent<OpponentMonsterTargetAsset>(OpponentMonsterPath);
        ModifyAttackContinuousAsset modifyAttack =
            CreateComponent<ModifyAttackContinuousAsset>(ModifyAttackPath);
        ModifyDefenseContinuousAsset modifyDefense =
            CreateComponent<ModifyDefenseContinuousAsset>(ModifyDefensePath);
        ReduceAttackUntilEndOfTurnResolutionAsset reduceAttack =
            CreateComponent<ReduceAttackUntilEndOfTurnResolutionAsset>(
                ReduceAttackPath);
        NegatePreviousActivationResolutionAsset negatePrevious =
            CreateComponent<NegatePreviousActivationResolutionAsset>(
                NegatePreviousPath);
        IncreaseAttackUntilEndOfTurnResolutionAsset increaseAttack =
            CreateComponent<IncreaseAttackUntilEndOfTurnResolutionAsset>(
                IncreaseAttackPath);
        IncreaseDefenseUntilEndOfTurnResolutionAsset increaseDefense =
            CreateComponent<IncreaseDefenseUntilEndOfTurnResolutionAsset>(
                IncreaseDefensePath);
        RestoreMonsterHealthResolutionAsset restoreHealth =
            CreateComponent<RestoreMonsterHealthResolutionAsset>(
                RestoreHealthPath);
        IncreaseDefensePermanentlyResolutionAsset permanentDefense =
            CreateComponent<IncreaseDefensePermanentlyResolutionAsset>(
                PermanentDefensePath);

        EnsureTideSpriteExists();

        ConfigureIronBlade(payEnergy, ownMonster, modifyAttack);
        ConfigureTestField(payEnergy, modifyAttack, modifyDefense);
        ConfigureAmbush(reduceAttack);
        ConfigureDivineReversal(payEnergy, negatePrevious);
        ConfigureMonsterSamples(
            payEnergy,
            CreateComponent<DrawCardsResolutionAsset>(DrawCardsPath),
            increaseAttack,
            permanentDefense,
            restoreHealth);
        ConfigureQuickStrike(payEnergy, ownMonster, increaseAttack);
        ConfigureRequestedCards(
            ownMonster,
            CreateComponent<OpponentMonsterTargetAsset>(OpponentMonsterPath));
        ApplyNovemberBalanceInternal();
    }

    // Targeted, repeatable migration. Preserve artwork, IDs, ranks, timing,
    // targets, costs and usage limits; do not regenerate scenes or card prefabs.
    [MenuItem("Card Game/Sample Cards/Apply November 3 Balance")]
    public static void ApplyNovemberBalance()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Apply card balance in Edit Mode only.");
        ApplyNovemberBalanceInternal();
        // Save only this migration's targets, not unrelated dirty assets in the Editor.
        foreach (string id in new[] { "dawnwing_fairy", "gravebound_imp", "sample_tide_sprite",
            "sample_stone_guardian", "fire_dragon", "sample_sky_sage", "legendary_fighter", "guardian_shift" })
        {
            CardData card = FindCardById(id);
            if (card != null) AssetDatabase.SaveAssetIfDirty(card);
        }
        AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<ModifyMaximumHealthResolutionAsset>(
            ResolutionFolder + "/Increase_Maximum_Health.asset"));
        Debug.Log("November 3 balance applied to 7 Monsters and Guardian Shift.");
    }

    private static void ApplyNovemberBalanceInternal()
    {
        SetBalancedStats("dawnwing_fairy", 700, 400, 150);
        SetBalancedStats("gravebound_imp", 800, 500, 200);
        SetBalancedStats("sample_tide_sprite", 900, 450, 200);
        SetBalancedStats("sample_stone_guardian", 1000, 350, 450);
        SetBalancedStats("fire_dragon", 1600, 1000, 300);
        SetBalancedStats("sample_sky_sage", 2500, 1400, 600);
        SetBalancedStats("legendary_fighter", 3500, 1000, 800);
        ModifyMaximumHealthResolutionAsset maximumHealth =
            CreateComponent<ModifyMaximumHealthResolutionAsset>(ResolutionFolder + "/Increase_Maximum_Health.asset");
        UpdateBalancedEffect("sample_tide_sprite",
            "Once per turn: Pay 1 Energy; this card gains 300 maximum HP, preserving damage already taken.",
            300, maximumHealth);
        UpdateBalancedEffect("sample_stone_guardian",
            "Once per turn: Pay 1 Energy; this card gains 300 DEF.", 300);
        UpdateBalancedEffect("guardian_shift",
            "When an opponent's monster attacks a monster you control: That monster gains 500 DEF. " +
            "If it is in Attack Position, change it to Defense Position, then remove all its Energy.", 500);
        CardData fighter = FindCardById("legendary_fighter");
        if (fighter != null)
        {
            SerializedObject card = new SerializedObject(fighter);
            SetString(card, "effectText", "When summoned, gains ATK equal to the original ATK of its 3 tributes. " +
                "When it KOs a monster by battle, remove all Energy from this card; recover 500 HP for each " +
                "Energy removed, up to its current maximum HP.");
            SaveCard(card, fighter);
        }
    }

    private static void SetBalancedStats(string id, int hp, int atk, int def)
    {
        CardData data = FindCardById(id);
        if (data == null) return;
        SerializedObject card = new SerializedObject(data);
        SetInt(card, "baseHealth", hp);
        SetInt(card, "baseAttack", atk);
        SetInt(card, "baseDefense", def);
        SaveCard(card, data);
    }

    private static void UpdateBalancedEffect(string id, string text, int amount,
        EffectResolutionAsset replacement = null)
    {
        CardData data = FindCardById(id);
        if (data == null) return;
        SerializedObject card = new SerializedObject(data);
        SerializedProperty effects = card.FindProperty("effects");
        if (effects.arraySize == 0) return;
        SerializedProperty resolution = effects.GetArrayElementAtIndex(0).FindPropertyRelative("resolution");
        if (resolution.arraySize == 0) return;
        SetString(card, "effectText", text);
        SerializedProperty slot = resolution.GetArrayElementAtIndex(0);
        slot.FindPropertyRelative("amount").intValue = amount;
        if (replacement != null) slot.FindPropertyRelative("component").objectReferenceValue = replacement;
        SaveCard(card, data);
    }

    private static void ConfigureIronBlade(
        PayEnergyCostAsset payEnergy,
        OwnMonsterTargetAsset ownMonster,
        ModifyAttackContinuousAsset modifyAttack)
    {
        CardData card = FindCardById("sample_iron_blade");
        if (card == null)
        {
            Debug.LogWarning("Iron Blade CardData was not found.");
            return;
        }

        SerializedObject serializedCard = new SerializedObject(card);
        SetString(
            serializedCard,
            "effectText",
            "Pay 1 Energy; equip to 1 monster you control. It gains 300 ATK.");
        SerializedProperty effects = ResetEffects(serializedCard, 1);
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        ConfigureEffectHeader(
            effect,
            "effect_iron_blade_equip",
            EffectKind.Continuous,
            EffectTriggerType.ManualActivation,
            EffectPhaseMask.Main,
            EffectTurnScope.YourTurn,
            EffectActivationWindow.OpenGameState);
        SetComponentSlot(effect, "costs", payEnergy, 1);
        SetComponentSlot(effect, "targets", ownMonster, 1);
        SetComponentSlot(effect, "continuousModifiers", modifyAttack, 300);
        SaveCard(serializedCard, card);
    }

    private static void ConfigureTestField(
        PayEnergyCostAsset payEnergy,
        ModifyAttackContinuousAsset modifyAttack,
        ModifyDefenseContinuousAsset modifyDefense)
    {
        CardData card = FindCardById("test_field_spell");
        if (card == null)
        {
            Debug.LogWarning("Test Field CardData was not found.");
            return;
        }

        SerializedObject serializedCard = new SerializedObject(card);
        SetString(
            serializedCard,
            "effectText",
            "Monsters you control gain 200 ATK and DEF. At the end of your " +
            "turn, pay 1 Energy or send this card to the Graveyard.");
        SerializedProperty effects = ResetEffects(serializedCard, 2);
        SerializedProperty fieldEffect = effects.GetArrayElementAtIndex(0);
        ConfigureEffectHeader(
            fieldEffect,
            "effect_test_field_boost",
            EffectKind.Continuous,
            EffectTriggerType.ManualActivation,
            EffectPhaseMask.Main,
            EffectTurnScope.YourTurn,
            EffectActivationWindow.OpenGameState);
        SetComponentSlots(
            fieldEffect,
            "continuousModifiers",
            new UnityEngine.Object[] { modifyAttack, modifyDefense },
            new[] { 200, 200 });

        SerializedProperty maintenance = effects.GetArrayElementAtIndex(1);
        ConfigureEffectHeader(
            maintenance,
            "effect_test_field_maintenance",
            EffectKind.Activated,
            EffectTriggerType.EndPhase,
            EffectPhaseMask.End,
            EffectTurnScope.YourTurn,
            EffectActivationWindow.OpenGameState);
        SetComponentSlot(maintenance, "costs", payEnergy, 1);
        SaveCard(serializedCard, card);
    }

    private static void ConfigureAmbush(
        ReduceAttackUntilEndOfTurnResolutionAsset reduceAttack)
    {
        CardData card = FindCardById("sample_ambush");
        if (card == null)
        {
            Debug.LogWarning("Ambush CardData was not found.");
            return;
        }

        SerializedObject serializedCard = new SerializedObject(card);
        SetString(
            serializedCard,
            "effectText",
            "When an opponent's monster declares an attack: That monster " +
            "loses 500 ATK until the end of this turn.");
        SerializedProperty effects = ResetEffects(serializedCard, 1);
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        ConfigureEffectHeader(
            effect,
            "effect_ambush_reduce_attacker",
            EffectKind.Activated,
            EffectTriggerType.OnAttackDeclared,
            EffectPhaseMask.Battle,
            EffectTurnScope.OpponentTurn,
            EffectActivationWindow.ResponseToAttackDeclaration);
        SetComponentSlot(effect, "targets", CreateComponent<OpponentMonsterTargetAsset>(
            OpponentMonsterPath), 1);
        SetComponentSlot(effect, "resolution", reduceAttack, 500);
        SaveCard(serializedCard, card);
    }

    private static void ConfigureDivineReversal(
        PayEnergyCostAsset payEnergy,
        NegatePreviousActivationResolutionAsset negatePrevious)
    {
        CardData card = FindCardById("sample_divine_reversal");
        if (card == null)
        {
            Debug.LogWarning("Divine Reversal CardData was not found.");
            return;
        }

        SerializedObject serializedCard = new SerializedObject(card);
        SetString(
            serializedCard,
            "effectText",
            "When your opponent activates a Spell or Trap: Pay 1 Energy; " +
            "negate that activation and send that card to the Graveyard.");
        SerializedProperty effects = ResetEffects(serializedCard, 1);
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        ConfigureEffectHeader(
            effect,
            "effect_divine_reversal_negate",
            EffectKind.Activated,
            EffectTriggerType.OnSpellTrapActivated,
            EffectPhaseMask.Any,
            EffectTurnScope.EitherTurn,
            EffectActivationWindow.ResponseToActivation);
        SetComponentSlot(effect, "costs", payEnergy, 1);
        SetComponentSlot(effect, "resolution", negatePrevious, 1);
        SaveCard(serializedCard, card);
    }

    private static void ConfigureMonsterSamples(
        PayEnergyCostAsset payEnergy,
        DrawCardsResolutionAsset drawCards,
        IncreaseAttackUntilEndOfTurnResolutionAsset increaseAttack,
        IncreaseDefensePermanentlyResolutionAsset permanentDefense,
        RestoreMonsterHealthResolutionAsset restoreHealth)
    {
        ConfigureSingleActivatedCard(
            "fire_dragon",
            "Pay 1 Energy: This card gains 300 ATK until the end of this turn.",
            "effect_fire_dragon_flame_charge",
            payEnergy,
            1,
            increaseAttack,
            300);
        ConfigureSingleActivatedCard(
            "sample_stone_guardian",
            "Pay 1 Energy: This card gains 500 DEF.",
            "effect_stone_guardian_fortify",
            payEnergy,
            1,
            permanentDefense,
            500);
        ConfigureSingleActivatedCard(
            "sample_sky_sage",
            "Pay 2 Energy: Draw 1 card.",
            "effect_sky_sage_insight",
            payEnergy,
            2,
            drawCards,
            1);
        ConfigureSingleActivatedCard(
            "sample_tide_sprite",
            "Pay 1 Energy: This card gains 300 HP.",
            "effect_tide_sprite_restore",
            payEnergy,
            1,
            restoreHealth,
            300);
    }

    private static void ConfigureQuickStrike(
        PayEnergyCostAsset payEnergy,
        OwnMonsterTargetAsset ownMonster,
        IncreaseAttackUntilEndOfTurnResolutionAsset increaseAttack)
    {
        CardData card = FindCardById("sample_quick_strike");
        if (card == null)
        {
            Debug.LogWarning("Quick Strike CardData was not found.");
            return;
        }

        SerializedObject serializedCard = new SerializedObject(card);
        SetString(
            serializedCard,
            "effectText",
            "Pay 1 Energy; target 1 monster you control. It gains 300 ATK " +
            "until the end of this turn.");
        SerializedProperty effects = ResetEffects(serializedCard, 1);
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        ConfigureEffectHeader(
            effect,
            "effect_quick_strike_attack_boost",
            EffectKind.Activated,
            EffectTriggerType.ManualActivation,
            EffectPhaseMask.Main,
            EffectTurnScope.YourTurn,
            EffectActivationWindow.OpenGameState);
        SetComponentSlot(effect, "costs", payEnergy, 1);
        SetComponentSlot(effect, "targets", ownMonster, 1);
        SetComponentSlot(effect, "resolution", increaseAttack, 300);
        SaveCard(serializedCard, card);
    }

    private static void ConfigureSingleActivatedCard(
        string cardId,
        string effectText,
        string effectId,
        PayEnergyCostAsset payEnergy,
        int energyCost,
        EffectResolutionAsset resolution,
        int amount)
    {
        CardData card = FindCardById(cardId);
        if (card == null)
        {
            Debug.LogWarning("Playable CardData was not found: " + cardId);
            return;
        }

        SerializedObject serializedCard = new SerializedObject(card);
        SetString(serializedCard, "effectText", effectText);
        SerializedProperty effects = ResetEffects(serializedCard, 1);
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        ConfigureEffectHeader(
            effect,
            effectId,
            EffectKind.Activated,
            EffectTriggerType.ManualActivation,
            EffectPhaseMask.Main,
            EffectTurnScope.YourTurn,
            EffectActivationWindow.OpenGameState);
        effect.FindPropertyRelative("maxUses").intValue = 1;
        SetComponentSlot(effect, "costs", payEnergy, energyCost);
        SetComponentSlot(effect, "resolution", resolution, amount);
        SaveCard(serializedCard, card);
    }

    private static void EnsureRequestedCardsExist()
    {
        int created = 0;
        int skipped = 0;
        CreateCardIfMissing(
            MonstersFolder + "/Legendary_Fighter.asset",
            "legendary_fighter",
            card => ConfigureMonsterBase(
                card,
                "legendary_fighter",
                "Legendary Fighter",
                "When summoned, gains ATK equal to the original ATK of its " +
                "3 tributes. When it destroys a monster by battle, remove all " +
                "Energy from this card; it gains 500 HP for each Energy removed.",
                MonsterAttribute.Metal,
                MonsterRank.Ultimate,
                MonsterType.Warrior,
                3000,
                1000,
                2500,
                2),
            ref created,
            ref skipped);
        CreateCardIfMissing(
            MonstersFolder + "/Dawnwing_Fairy.asset",
            "dawnwing_fairy",
            card => ConfigureMonsterBase(
                card,
                "dawnwing_fairy",
                "Dawnwing Fairy",
                "When this card is summoned: You can add 1 Basic Monster from " +
                "your Deck to your hand, except Dawnwing Fairy, then shuffle.",
                MonsterAttribute.Wind,
                MonsterRank.Basic,
                MonsterType.Fairy,
                1000,
                800,
                800,
                1),
            ref created,
            ref skipped);
        CreateCardIfMissing(
            MonstersFolder + "/Gravebound_Imp.asset",
            "gravebound_imp",
            card => ConfigureMonsterBase(
                card,
                "gravebound_imp",
                "Gravebound Imp",
                "When this card is destroyed by battle: You can add 1 Basic " +
                "Monster from your Graveyard to your hand.",
                MonsterAttribute.Wood,
                MonsterRank.Basic,
                MonsterType.Fiend,
                1100,
                900,
                600,
                1),
            ref created,
            ref skipped);
        CreateSimpleSpell(
            SpellsFolder + "/Energy_Venom.asset",
            "energy_venom",
            "Energy Venom",
            SpellType.Equip,
            "Equip only to an opponent's monster. It loses 300 ATK for each " +
            "Energy allocated to it.",
            ref created,
            ref skipped);
        CreateSimpleSpell(
            SpellsFolder + "/Power_Transfer.asset",
            "power_transfer",
            "Power Transfer",
            SpellType.Normal,
            "Choose 2 different monsters you control in order. The first " +
            "loses all its ATK until the end of this turn, then the second " +
            "gains that amount until the end of this turn.",
            ref created,
            ref skipped);
        CreateSimpleSpell(
            SpellsFolder + "/Temporal_Lock.asset",
            "temporal_lock",
            "Temporal Lock",
            SpellType.Normal,
            "Skip your opponent's next Draw Step, and skip your Battle Phase " +
            "this turn.",
            ref created,
            ref skipped);
        CreateCardIfMissing(
            TrapsFolder + "/Guardian_Shift.asset",
            "guardian_shift",
            card =>
            {
                SetCommonData(
                    card,
                    "guardian_shift",
                    "Guardian Shift",
                    CardCategory.Trap,
                    "When an opponent's monster attacks a monster you control: " +
                    "That monster gains 1000 DEF. If it is in Attack Position, " +
                    "change it to Defense Position, then remove all its Energy.");
                SetEnum(card, "trapType", TrapType.Normal);
            },
            ref created,
            ref skipped);
    }

    private static void CreateSimpleSpell(
        string path,
        string id,
        string name,
        SpellType type,
        string text,
        ref int created,
        ref int skipped)
    {
        CreateCardIfMissing(
            path,
            id,
            card =>
            {
                SetCommonData(card, id, name, CardCategory.Spell, text);
                SetEnum(card, "spellType", type);
            },
            ref created,
            ref skipped);
    }

    private static void ConfigureMonsterBase(
        SerializedObject card,
        string id,
        string name,
        string text,
        MonsterAttribute attribute,
        MonsterRank rank,
        MonsterType type,
        int health,
        int attack,
        int defense,
        int attackEnergy)
    {
        SetCommonData(card, id, name, CardCategory.Monster, text);
        SetEnum(card, "monsterAttribute", attribute);
        SetEnum(card, "monsterRank", rank);
        SetEnum(card, "monsterType", type);
        SetInt(card, "baseHealth", health);
        SetInt(card, "baseAttack", attack);
        SetInt(card, "baseDefense", defense);
        SetInt(card, "attackEnergyRequirement", attackEnergy);
    }

    private static void ConfigureRequestedCards(
        OwnMonsterTargetAsset ownMonster,
        OpponentMonsterTargetAsset opponentMonster)
    {
        ConfigureLegendaryFighter();
        ConfigureEnergyVenom(opponentMonster);
        ConfigureGuardianShift(ownMonster);
        ConfigurePowerTransfer(ownMonster);
        ConfigureTemporalLock();
        ConfigureDawnwingFairy();
        ConfigureGraveboundImp();
    }

    private static void ConfigureLegendaryFighter()
    {
        CardData card = FindCardById("legendary_fighter");
        if (card == null)
            return;
        SerializedObject serialized = new SerializedObject(card);
        SerializedProperty effects = ResetEffects(serialized, 2);
        SerializedProperty summon = effects.GetArrayElementAtIndex(0);
        ConfigureEffectHeader(
            summon,
            "effect_legendary_fighter_tribute_power",
            EffectKind.Activated,
            EffectTriggerType.OnSummoned,
            EffectPhaseMask.Main,
            EffectTurnScope.YourTurn,
            EffectActivationWindow.OpenGameState);
        SetComponentSlot(
            summon,
            "resolution",
            CreateComponent<IncreaseAttackByTriggerValueResolutionAsset>(
                TriggerAttackPath),
            1);

        SerializedProperty victory = effects.GetArrayElementAtIndex(1);
        ConfigureEffectHeader(
            victory,
            "effect_legendary_fighter_victory_recovery",
            EffectKind.Activated,
            EffectTriggerType.OnBattleDestroyedMonster,
            EffectPhaseMask.Battle,
            EffectTurnScope.YourTurn,
            EffectActivationWindow.OpenGameState);
        SetComponentSlot(
            victory,
            "resolution",
            CreateComponent<RestoreHealthPerRemovedEnergyResolutionAsset>(
                EnergyHealthPath),
            500);
        SaveCard(serialized, card);
    }

    private static void ConfigureEnergyVenom(
        OpponentMonsterTargetAsset opponentMonster)
    {
        CardData card = FindCardById("energy_venom");
        if (card == null)
            return;
        SerializedObject serialized = new SerializedObject(card);
        SerializedProperty effect = ResetEffects(serialized, 1)
            .GetArrayElementAtIndex(0);
        ConfigureEffectHeader(
            effect,
            "effect_energy_venom_equip",
            EffectKind.Continuous,
            EffectTriggerType.ManualActivation,
            EffectPhaseMask.Main,
            EffectTurnScope.YourTurn,
            EffectActivationWindow.OpenGameState);
        SetComponentSlot(effect, "targets", opponentMonster, 1);
        SetComponentSlot(
            effect,
            "continuousModifiers",
            CreateComponent<ModifyAttackPerTargetEnergyContinuousAsset>(
                ModifyAttackPerEnergyPath),
            -300);
        SaveCard(serialized, card);
    }

    private static void ConfigureGuardianShift(OwnMonsterTargetAsset ownMonster)
    {
        CardData card = FindCardById("guardian_shift");
        if (card == null)
            return;
        SerializedObject serialized = new SerializedObject(card);
        SerializedProperty effect = ResetEffects(serialized, 1)
            .GetArrayElementAtIndex(0);
        ConfigureEffectHeader(
            effect,
            "effect_guardian_shift_protect",
            EffectKind.Activated,
            EffectTriggerType.OnAttackDeclared,
            EffectPhaseMask.Battle,
            EffectTurnScope.OpponentTurn,
            EffectActivationWindow.ResponseToAttackDeclaration);
        SetComponentSlot(effect, "targets", ownMonster, 1);
        SetComponentSlot(
            effect,
            "resolution",
            CreateComponent<ProtectAttackedMonsterResolutionAsset>(
                ProtectAttackedMonsterPath),
            1000);
        SaveCard(serialized, card);
    }

    private static void ConfigurePowerTransfer(OwnMonsterTargetAsset ownMonster)
    {
        CardData card = FindCardById("power_transfer");
        if (card == null)
            return;
        SerializedObject serialized = new SerializedObject(card);
        SerializedProperty effect = ResetEffects(serialized, 1)
            .GetArrayElementAtIndex(0);
        ConfigureEffectHeader(
            effect,
            "effect_power_transfer",
            EffectKind.Activated,
            EffectTriggerType.ManualActivation,
            EffectPhaseMask.Main,
            EffectTurnScope.YourTurn,
            EffectActivationWindow.OpenGameState);
        SetComponentSlot(effect, "targets", ownMonster, 2);
        SetComponentSlot(
            effect,
            "resolution",
            CreateComponent<TransferAttackUntilEndOfTurnResolutionAsset>(
                TransferAttackPath),
            1);
        SaveCard(serialized, card);
    }

    private static void ConfigureTemporalLock()
    {
        CardData card = FindCardById("temporal_lock");
        if (card == null)
            return;
        SerializedObject serialized = new SerializedObject(card);
        SerializedProperty effect = ResetEffects(serialized, 1)
            .GetArrayElementAtIndex(0);
        ConfigureEffectHeader(
            effect,
            "effect_temporal_lock",
            EffectKind.Activated,
            EffectTriggerType.ManualActivation,
            EffectPhaseMask.Main,
            EffectTurnScope.YourTurn,
            EffectActivationWindow.OpenGameState);
        SetComponentSlots(
            effect,
            "resolution",
            new UnityEngine.Object[]
            {
                CreateComponent<SkipNextOpponentDrawResolutionAsset>(SkipDrawPath),
                CreateComponent<SkipCurrentBattlePhaseResolutionAsset>(SkipBattlePath)
            },
            new[] { 1, 1 });
        SaveCard(serialized, card);
    }

    private static void ConfigureDawnwingFairy()
    {
        ConfigureTriggeredMonsterSelection(
            "dawnwing_fairy",
            "effect_dawnwing_fairy_search",
            EffectTriggerType.OnSummoned,
            CreateComponent<SearchBasicMonsterFromDeckResolutionAsset>(
                SearchBasicPath));
    }

    private static void ConfigureGraveboundImp()
    {
        ConfigureTriggeredMonsterSelection(
            "gravebound_imp",
            "effect_gravebound_imp_recover",
            EffectTriggerType.OnDestroyed,
            CreateComponent<RecoverBasicMonsterFromGraveyardResolutionAsset>(
                RecoverBasicPath));
    }

    private static void ConfigureTriggeredMonsterSelection(
        string cardId,
        string effectId,
        EffectTriggerType trigger,
        EffectResolutionAsset resolution)
    {
        CardData card = FindCardById(cardId);
        if (card == null)
            return;
        SerializedObject serialized = new SerializedObject(card);
        SerializedProperty effect = ResetEffects(serialized, 1)
            .GetArrayElementAtIndex(0);
        ConfigureEffectHeader(
            effect,
            effectId,
            EffectKind.Activated,
            trigger,
            EffectPhaseMask.Any,
            EffectTurnScope.EitherTurn,
            EffectActivationWindow.OpenGameState);
        SetComponentSlot(effect, "resolution", resolution, 1);
        SaveCard(serialized, card);
    }

    private static void EnsureTideSpriteExists()
    {
        if (FindCardById("sample_tide_sprite") != null)
            return;

        CardData card = ScriptableObject.CreateInstance<CardData>();
        SerializedObject serializedCard = new SerializedObject(card);
        ConfigureTideSpriteBaseData(serializedCard);
        serializedCard.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(
            card,
            MonstersFolder + "/Sample_Tide_Sprite.asset");
        EditorUtility.SetDirty(card);
    }

    private static void ConfigureTideSpriteBaseData(SerializedObject card)
    {
        SetCommonData(
            card,
            "sample_tide_sprite",
            "Tide Sprite",
            CardCategory.Monster,
            "Pay 1 Energy: This card gains 300 HP.");
        SetEnum(card, "monsterAttribute", MonsterAttribute.Water);
        SetEnum(card, "monsterRank", MonsterRank.Basic);
        SetEnum(card, "monsterType", MonsterType.Spirit);
        SetInt(card, "baseHealth", 1000);
        SetInt(card, "baseAttack", 600);
        SetInt(card, "baseDefense", 900);
        SetInt(card, "attackEnergyRequirement", 1);
    }

    private static SerializedProperty ResetEffects(
        SerializedObject serializedCard,
        int count)
    {
        SerializedProperty effects = serializedCard.FindProperty("effects");
        effects.ClearArray();
        effects.arraySize = count;
        return effects;
    }

    private static void ConfigureEffectHeader(
        SerializedProperty effect,
        string effectId,
        EffectKind kind,
        EffectTriggerType trigger,
        EffectPhaseMask phases,
        EffectTurnScope turnScope,
        EffectActivationWindow window)
    {
        effect.FindPropertyRelative("effectId").stringValue = effectId;
        effect.FindPropertyRelative("effectKind").enumValueIndex = (int)kind;
        effect.FindPropertyRelative("trigger").enumValueIndex = (int)trigger;
        effect.FindPropertyRelative("allowedPhases").intValue = (int)phases;
        effect.FindPropertyRelative("allowedTurn").enumValueIndex =
            (int)turnScope;
        effect.FindPropertyRelative("activationWindow").enumValueIndex =
            (int)window;
        effect.FindPropertyRelative("activationSource").enumValueIndex =
            (int)EffectActivationSource.Standard;
        effect.FindPropertyRelative("conditions").ClearArray();
        effect.FindPropertyRelative("costs").ClearArray();
        effect.FindPropertyRelative("targets").ClearArray();
        effect.FindPropertyRelative("resolution").ClearArray();
        effect.FindPropertyRelative("continuousModifiers").ClearArray();
        effect.FindPropertyRelative("maxUses").intValue = 0;
        effect.FindPropertyRelative("countBy").enumValueIndex =
            (int)EffectUsageScope.PerInstance;
        effect.FindPropertyRelative("reset").enumValueIndex =
            (int)EffectUsageReset.PerTurn;
    }

    private static void SetComponentSlot(
        SerializedProperty effect,
        string listName,
        UnityEngine.Object component,
        int amount)
    {
        SetComponentSlots(
            effect,
            listName,
            new[] { component },
            new[] { amount });
    }

    private static void SetComponentSlots(
        SerializedProperty effect,
        string listName,
        IReadOnlyList<UnityEngine.Object> components,
        IReadOnlyList<int> amounts)
    {
        SerializedProperty list = effect.FindPropertyRelative(listName);
        list.arraySize = components.Count;
        for (int index = 0; index < components.Count; index++)
        {
            SerializedProperty slot = list.GetArrayElementAtIndex(index);
            slot.FindPropertyRelative("component").objectReferenceValue =
                components[index];
            slot.FindPropertyRelative("amount").intValue = amounts[index];
        }
    }

    private static void SaveCard(
        SerializedObject serializedCard,
        CardData card)
    {
        serializedCard.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(card);
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
