using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit create-once command. Never rewrites existing cards, presets or player saves.
public static class StrategicCardExpansionTool
{
    private const string CardRoot = "Assets/Data/Cards/Expansion";
    private const string ComponentRoot = "Assets/Data/EffectComponents/Expansion";
    private const string AppScene = "Assets/Scenes/AppShell.unity";
    private static string Stamp => Path.GetFullPath(Path.Combine(Application.dataPath,
        "../Tools/Simulation/Control/StrategicCards/created-v4.json"));

    [MenuItem("Card Game/Cards/Create Strategic Expansion v4")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Stop Play and finish compilation first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save your open scenes before creating the expansion.");
        if (File.Exists(Stamp))
            throw new InvalidOperationException("Expansion v4 was already created. Existing cards will not be regenerated.");
        if (!File.Exists(Path.Combine(Application.dataPath, "../Tools/Simulation/Control/ExistingCardBalance/applied-v3.json")))
            throw new InvalidOperationException("Apply Existing Card Balance v3 first.");

        int checks = StrategicCardExpansionChecks.Run();
        EnsureFolder(CardRoot); EnsureFolder(ComponentRoot);
        var cards = new List<CardData>();
        foreach (CardDefinition definition in StrategicCardExpansionCatalog.Create())
        {
            string path = CardPath(definition.CardId);
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardData>();
                var data = new SerializedObject(card);
                data.FindProperty("cardId").stringValue = definition.CardId;
                data.FindProperty("cardName").stringValue = definition.CardName;
                data.FindProperty("category").enumValueIndex = (int)definition.Category;
                data.FindProperty("effectText").stringValue = definition.EffectText;
                data.FindProperty("copyLimitOverride").intValue = definition.CopyLimitOverride;
                data.FindProperty("monsterAttribute").enumValueIndex = (int)definition.MonsterAttribute;
                data.FindProperty("monsterRank").enumValueIndex = (int)definition.MonsterRank;
                data.FindProperty("monsterType").enumValueIndex = (int)definition.MonsterType;
                data.FindProperty("baseHealth").intValue = definition.BaseHealth;
                data.FindProperty("baseAttack").intValue = definition.BaseAttack;
                data.FindProperty("baseDefense").intValue = definition.BaseDefense;
                data.FindProperty("attackEnergyRequirement").intValue = definition.AttackEnergyRequirement;
                data.FindProperty("spellType").enumValueIndex = (int)definition.SpellType;
                data.FindProperty("trapType").enumValueIndex = (int)definition.TrapType;
                WriteEffects(data.FindProperty("effects"), definition.Effects);
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(card, path);
                AssetDatabase.SaveAssetIfDirty(card);
            }
            if (card.CardId != definition.CardId)
                throw new InvalidOperationException("A different card already exists at " + path);
            cards.Add(card);
        }
        VerifyAssets();
        RegisterCatalog(cards);
        SimulationCatalog simulation = AssetDatabase.LoadAssetAtPath<SimulationCatalog>("Assets/Simulation/SimulationCatalog.asset");
        if (simulation != null)
        {
            simulation.cards = (simulation.cards ?? new CardData[0]).Concat(cards)
                .Where(card => card != null).GroupBy(card => card.CardId).Select(group => group.First()).ToArray();
            simulation.sourceFingerprint = ""; simulation.catalogFingerprint = "";
            EditorUtility.SetDirty(simulation); AssetDatabase.SaveAssetIfDirty(simulation);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Stamp));
        File.WriteAllText(Stamp, "{\"version\":4,\"cards\":" + cards.Count + ",\"checks\":" + checks +
            ",\"createdUtc\":\"" + DateTime.UtcNow.ToString("O") + "\"}");
        Debug.Log("Strategic expansion v4 PASS: " + cards.Count + " new cards, " + checks +
            " rules checks. Added to Deck Builder; existing cards and default decks preserved. Artwork is unassigned.");
    }

    [MenuItem("Card Game/Cards/Verify Strategic Expansion v4")]
    public static void Verify()
    {
        int checks = StrategicCardExpansionChecks.Run(); VerifyAssets();
        Debug.Log("Strategic expansion v4 verification PASS: 24 card definitions/assets and " + checks + " rules checks.");
    }

    private static string CardPath(string id) => CardRoot + "/" +
        string.Join("_", id.Split('_').Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1))) + ".asset";

    private static void VerifyAssets()
    {
        foreach (CardDefinition expected in StrategicCardExpansionCatalog.Create())
        {
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(CardPath(expected.CardId));
            if (card == null) throw new InvalidOperationException("Missing new card: " + expected.CardName);
            CardDefinition actual = CardDataConverter.ToDefinition(card);
            if (actual.CardId != expected.CardId || actual.Effects.Any(effect => effect.MaxUses != 0) ||
                actual.CopyLimitOverride != expected.CopyLimitOverride)
                throw new InvalidOperationException("Expansion identity/usage/copy limit mismatch: " + expected.CardName);
        }
    }

    private static void WriteEffects(SerializedProperty array, IReadOnlyList<EffectDefinition> effects)
    {
        array.arraySize = effects.Count;
        for (int i = 0; i < effects.Count; i++)
        {
            EffectDefinition effect = effects[i]; SerializedProperty value = array.GetArrayElementAtIndex(i);
            value.FindPropertyRelative("effectId").stringValue = effect.EffectId;
            value.FindPropertyRelative("effectKind").enumValueIndex = (int)effect.Kind;
            value.FindPropertyRelative("trigger").enumValueIndex = (int)effect.Trigger;
            value.FindPropertyRelative("allowedPhases").intValue = (int)effect.AllowedPhases;
            value.FindPropertyRelative("allowedTurn").enumValueIndex = (int)effect.AllowedTurn;
            value.FindPropertyRelative("activationWindow").enumValueIndex = (int)effect.ActivationWindow;
            value.FindPropertyRelative("activationSource").enumValueIndex = (int)effect.ActivationSource;
            value.FindPropertyRelative("maxUses").intValue = 0;
            value.FindPropertyRelative("countBy").enumValueIndex = (int)EffectUsageScope.PerInstance;
            value.FindPropertyRelative("reset").enumValueIndex = (int)EffectUsageReset.PerTurn;
            var conditions = effect.Conditions.Select(item => (Asset: (UnityEngine.Object)Condition(item), Amount: item.Amount)).ToArray();
            Slots(value.FindPropertyRelative("conditions"), conditions);
            Slots(value.FindPropertyRelative("costs"), effect.Costs.Select(item =>
                (Asset: (UnityEngine.Object)Cost(item), Amount: item.Amount)).ToArray());
            Slots(value.FindPropertyRelative("targets"), effect.TargetType == EffectTargetType.None
                ? new (UnityEngine.Object Asset, int Amount)[0] : new[] { ((UnityEngine.Object)Target(effect.TargetType), effect.TargetCount) });
            Slots(value.FindPropertyRelative("resolution"), effect.Operations.Select(item =>
                (Asset: (UnityEngine.Object)Resolution(item), Amount: item.Amount)).ToArray());
            var modifiers = new List<(UnityEngine.Object Asset, int Amount)>();
            foreach (IContinuousModifier modifier in effect.ContinuousModifiers)
            {
                var aura = modifier as PositionAuraModifier;
                if (aura == null) throw new InvalidOperationException("Unsupported new-card modifier.");
                modifiers.Add((Aura(aura.IsDefense), aura.Amount));
            }
            Slots(value.FindPropertyRelative("continuousModifiers"), modifiers.ToArray());
        }
    }

    private static void Slots(SerializedProperty array, (UnityEngine.Object Asset, int Amount)[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            SerializedProperty slot = array.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("component").objectReferenceValue = values[i].Asset;
            slot.FindPropertyRelative("amount").intValue = values[i].Amount;
        }
    }

    private static T Component<T>(string name, Action<SerializedObject> configure) where T : ScriptableObject
    {
        string path = ComponentRoot + "/" + name + ".asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>(); var data = new SerializedObject(asset);
        configure(data); data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    private static ExpansionConditionAsset Condition(EffectConditionDefinition item)
        => Component<ExpansionConditionAsset>("Condition_" + item.Type,
            data => data.FindProperty("condition").enumValueIndex = (int)item.Type);
    private static ExpansionTargetAsset Target(EffectTargetType type)
        => Component<ExpansionTargetAsset>("Target_" + type,
            data => data.FindProperty("target").enumValueIndex = (int)type);
    private static ExpansionCostAsset Cost(EffectCostDefinition item)
        => Component<ExpansionCostAsset>("Cost_" + item.Type + "_" + item.SecondaryAmount, data =>
        { data.FindProperty("cost").enumValueIndex = (int)item.Type; data.FindProperty("secondaryAmount").intValue = item.SecondaryAmount; });
    private static ExpansionResolutionAsset Resolution(EffectOperationDefinition item)
        => Component<ExpansionResolutionAsset>("Resolution_" + item.Type + "_" + item.SecondaryAmount, data =>
        { data.FindProperty("operation").enumValueIndex = (int)item.Type; data.FindProperty("secondaryAmount").intValue = item.SecondaryAmount; });
    private static PositionAuraModifierAsset Aura(bool defense)
        => Component<PositionAuraModifierAsset>(defense ? "Aura_Defense" : "Aura_Attack",
            data => data.FindProperty("defense").boolValue = defense);

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void RegisterCatalog(IReadOnlyList<CardData> cards)
    {
        Scene scene = SceneManager.GetSceneByPath(AppScene);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        Scene previous = SceneManager.GetActiveScene();
        if (opened) scene = EditorSceneManager.OpenScene(AppScene, OpenSceneMode.Additive);
        try
        {
            DeckBuilderAppController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<DeckBuilderAppController>(true)).FirstOrDefault();
            if (controller == null) throw new InvalidOperationException("AppShell catalog controller is missing.");
            var data = new SerializedObject(controller);
            SerializedProperty catalog = data.FindProperty("catalogCards");
            SerializedProperty times = data.FindProperty("catalogUpdatedTicks");
            times.arraySize = catalog.arraySize;
            foreach (CardData card in cards)
            {
                bool present = false;
                for (int i = 0; i < catalog.arraySize; i++)
                    if (catalog.GetArrayElementAtIndex(i).objectReferenceValue == card) { present = true; break; }
                if (present) continue;
                int index = catalog.arraySize++; times.arraySize = catalog.arraySize;
                catalog.GetArrayElementAtIndex(index).objectReferenceValue = card;
                times.GetArrayElementAtIndex(index).longValue = File.GetLastWriteTimeUtc(AssetDatabase.GetAssetPath(card)).Ticks;
            }
            data.ApplyModifiedProperties(); EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
}
