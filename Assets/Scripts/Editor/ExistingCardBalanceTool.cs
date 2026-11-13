using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Explicit, one-shot update of existing cards. No automatic import or sample regeneration.
public static class ExistingCardBalanceTool
{
    private const string Menu = "Card Game/Balance/Apply Existing Card Balance v3";
    private const string Cards = "Assets/Data/Cards/";
    private static string Stamp => Path.Combine(Application.dataPath, "../Tools/Simulation/Control/ExistingCardBalance/applied-v3.json");

    [MenuItem(Menu)]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Stop Play and finish compilation before applying card balance.");
        if (File.Exists(Stamp))
            throw new InvalidOperationException("Balance v3 was already applied. No cards were changed; use Verify Existing Card Balance v3.");
        int checks = ExistingCardBalanceChecks.Run();
        const string conditions = "Assets/Data/EffectComponents/Conditions";
        if (!AssetDatabase.IsValidFolder(conditions))
            AssetDatabase.CreateFolder("Assets/Data/EffectComponents", "Conditions");
        var defense = LoadOrCreate<SourceInDefenseConditionAsset>(conditions + "/Source_In_Defense.asset");
        var afterFirst = LoadOrCreate<AfterFirstTurnConditionAsset>(conditions + "/After_First_Turn.asset");

        Edit("Monsters/Dawnwing_Fairy.asset", "When this card is summoned: You can add 1 Basic Monster from your Deck to your hand, except Dawnwing Fairy, then shuffle your Deck. " + Limit("Dawnwing Fairy"),
            effect => LimitByName(effect));
        Edit("Monsters/Sample_Tide_Sprite.asset", "During your End Phase, if this card is in Defense Position: It gains 200 maximum HP, preserving damage already taken.",
            effect => Growth(effect, defense));
        Edit("Monsters/Sample_Stone_Guardian.asset", "During your End Phase, if this card is in Defense Position: It gains 100 DEF.",
            effect => Growth(effect, defense));
        Edit("Monsters/Fire_Dragon.asset", "Once per turn: Pay 1 Energy; this card gains 300 ATK until the end of this turn.", effect => { });
        Edit("Monsters/Sample_Sky_Sage.asset", "Once per turn: Pay 2 Energy; draw 2 cards.", effect => { });
        Edit("Monsters/Legendary_Fighter.asset", "When summoned, gains ATK equal to the original ATK of its 3 tributes. When this card KOs a monster by battle: You can remove all Energy from this card; recover 500 HP for each Energy removed, up to its current maximum HP.", effect => { });
        Edit("Spells/Sample_Arcane_Insight.asset", "Pay 1 Energy; draw 2 cards. " + Limit("Arcane Insight"), effect =>
        {
            LimitByName(effect);
            effect.FindPropertyRelative("resolution").GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 2;
        });
        Edit("Spells/Sample_Quick_Strike.asset", "Target 1 monster you control; it gains 300 ATK until the end of this turn. " + Limit("Quick Strike"), effect =>
        {
            LimitByName(effect);
            effect.FindPropertyRelative("costs").ClearArray();
        });
        Edit("Spells/Test_Field_Spell.asset", "Monsters you control gain 200 ATK and DEF. During your End Phase: Pay 1 Energy or send this card to the Graveyard.", effect => { });
        Edit("Spells/Temporal_Lock.asset", "You cannot activate this card on the first turn of the match. Skip your opponent's next Draw Step, and skip your Battle Phase this turn. " + Limit("Temporal Lock"), effect =>
        {
            LimitByName(effect);
            SetCondition(effect, afterFirst);
        });
        Edit("Traps/Guardian_Shift.asset", "When an opponent's monster attacks a monster you control: That monster gains 500 DEF until the end of this turn. If it is in Attack Position, change it to Defense Position, then remove all its Energy. " + Limit("Guardian Shift"), effect => LimitByName(effect));

        VerifyAssets();
        Directory.CreateDirectory(Path.GetDirectoryName(Stamp));
        File.WriteAllText(Stamp, "{\"balanceVersion\":3,\"appliedUtc\":\"" + DateTime.UtcNow.ToString("O") + "\",\"checks\":" + checks + "}");
        Debug.Log("Existing card balance v3 PASS: " + checks + " rule checks; 11 existing card assets updated, base stats and attack Energy preserved. No new cards created.");
    }

    [MenuItem("Card Game/Balance/Verify Existing Card Balance v3")]
    public static void Verify()
    {
        int checks = ExistingCardBalanceChecks.Run();
        VerifyAssets();
        Debug.Log("Existing card balance v3 verification PASS: " + checks + " rule checks and authored card data/text.");
    }

    private static string Limit(string name) => "You can only use this effect of " + name + " once per turn.";

    private static void LimitByName(SerializedProperty effect)
    {
        effect.FindPropertyRelative("maxUses").intValue = 1;
        effect.FindPropertyRelative("countBy").enumValueIndex = (int)EffectUsageScope.PerCardId;
        effect.FindPropertyRelative("reset").enumValueIndex = (int)EffectUsageReset.PerTurn;
    }

    private static void Growth(SerializedProperty effect, EffectConditionAsset condition)
    {
        effect.FindPropertyRelative("trigger").enumValueIndex = (int)EffectTriggerType.EndPhase;
        effect.FindPropertyRelative("allowedPhases").intValue = (int)EffectPhaseMask.End;
        effect.FindPropertyRelative("costs").ClearArray();
        SetCondition(effect, condition);
    }

    private static void SetCondition(SerializedProperty effect, EffectConditionAsset condition)
    {
        SerializedProperty slots = effect.FindPropertyRelative("conditions");
        slots.arraySize = 1;
        slots.GetArrayElementAtIndex(0).FindPropertyRelative("component").objectReferenceValue = condition;
        slots.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 0;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    private static void Edit(string relative, string text, Action<SerializedProperty> edit)
    {
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(Cards + relative);
        if (card == null) throw new InvalidOperationException("Missing existing card: " + relative);
        string stats = Stats(card);
        var serialized = new SerializedObject(card);
        serialized.FindProperty("effectText").stringValue = text;
        edit(serialized.FindProperty("effects").GetArrayElementAtIndex(0));
        serialized.ApplyModifiedProperties();
        if (Stats(card) != stats) throw new InvalidOperationException("Base stats changed unexpectedly: " + relative);
        CardDataConverter.ToDefinition(card);
        AssetDatabase.SaveAssetIfDirty(card);
    }

    private static string Stats(CardData card) => card.CardId + ":" + card.BaseHealth + ":" +
        card.BaseAttack + ":" + card.BaseDefense + ":" + card.AttackEnergyRequirement;

    private static void VerifyAssets()
    {
        foreach (string relative in new[] { "Monsters/Dawnwing_Fairy.asset", "Spells/Sample_Arcane_Insight.asset",
            "Spells/Sample_Quick_Strike.asset", "Spells/Temporal_Lock.asset", "Traps/Guardian_Shift.asset" })
        {
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(Cards + relative);
            EffectDefinition effect = CardDataConverter.ToDefinition(card).Effects[0];
            if (effect.MaxUses != 1 || effect.CountBy != EffectUsageScope.PerCardId ||
                effect.Reset != EffectUsageReset.PerTurn || !card.EffectText.EndsWith(Limit(card.CardName), StringComparison.Ordinal))
                throw new InvalidOperationException("Name limit/data/text mismatch: " + relative);
        }
        foreach (string relative in new[] { "Monsters/Sample_Tide_Sprite.asset", "Monsters/Sample_Stone_Guardian.asset" })
        {
            EffectDefinition effect = CardDataConverter.ToDefinition(AssetDatabase.LoadAssetAtPath<CardData>(Cards + relative)).Effects[0];
            if (effect.Trigger != EffectTriggerType.EndPhase || effect.Costs.Count != 0 ||
                effect.Conditions.Count != 1 || effect.Conditions[0].Type != EffectConditionType.SourceInDefense)
                throw new InvalidOperationException("End-phase growth mismatch: " + relative);
        }
        var insight = CardDataConverter.ToDefinition(AssetDatabase.LoadAssetAtPath<CardData>(Cards + "Spells/Sample_Arcane_Insight.asset"));
        var quick = CardDataConverter.ToDefinition(AssetDatabase.LoadAssetAtPath<CardData>(Cards + "Spells/Sample_Quick_Strike.asset"));
        if (insight.Effects[0].Operations[0].Amount != 2 || quick.Effects[0].Costs.Count != 0)
            throw new InvalidOperationException("Draw amount or free Quick Strike mismatch.");
    }
}
