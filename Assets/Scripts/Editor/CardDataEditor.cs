using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardData))]
public class CardDataEditor : Editor
{
    private CardData stagedCard;
    private SerializedObject stagedObject;
    private string stagedBaseline;
    private string assetBaseline;
    private bool assetWasDirtyAtOpen;
    private SerializedProperty cardId;
    private SerializedProperty cardName;
    private SerializedProperty category;
    private SerializedProperty artwork;
    private SerializedProperty effectText;
    private SerializedProperty monsterAttribute;
    private SerializedProperty monsterRank;
    private SerializedProperty monsterType;
    private SerializedProperty baseHealth;
    private SerializedProperty baseAttack;
    private SerializedProperty baseDefense;
    private SerializedProperty attackEnergyRequirement;
    private SerializedProperty spellType;
    private SerializedProperty trapType;
    private SerializedProperty effects;

    private void OnEnable()
    {
        if (target == null)
            return;

        stagedCard = CreateInstance<CardData>();
        EditorUtility.CopySerializedManagedFieldsOnly(target, stagedCard);
        stagedCard.name = target.name;
        // HideAndDontSave also sets NotEditable, which disables every field.
        stagedCard.hideFlags = HideFlags.DontSave;
        stagedObject = new SerializedObject(stagedCard);
        stagedBaseline = EditorJsonUtility.ToJson(stagedCard);
        assetBaseline = EditorJsonUtility.ToJson(target);
        assetWasDirtyAtOpen = EditorUtility.IsDirty(target);
        BindProperties();
    }

    private void OnDisable()
    {
        if (stagedCard != null)
            DestroyImmediate(stagedCard);
    }

    private void BindProperties()
    {
        cardId = stagedObject.FindProperty("cardId");
        cardName = stagedObject.FindProperty("cardName");
        category = stagedObject.FindProperty("category");
        artwork = stagedObject.FindProperty("artwork");
        effectText = stagedObject.FindProperty("effectText");
        monsterAttribute = stagedObject.FindProperty("monsterAttribute");
        monsterRank = stagedObject.FindProperty("monsterRank");
        monsterType = stagedObject.FindProperty("monsterType");
        baseHealth = stagedObject.FindProperty("baseHealth");
        baseAttack = stagedObject.FindProperty("baseAttack");
        baseDefense = stagedObject.FindProperty("baseDefense");
        attackEnergyRequirement =
            stagedObject.FindProperty("attackEnergyRequirement");
        spellType = stagedObject.FindProperty("spellType");
        trapType = stagedObject.FindProperty("trapType");
        effects = stagedObject.FindProperty("effects");
    }

    public override void OnInspectorGUI()
    {
        if (stagedObject == null)
            return;

        stagedObject.Update();

        EditorGUILayout.HelpBox(
            "Edit this draft, then click Save Card Changes. Cancel discards " +
            "edits made in this Inspector. Ctrl+S alone does not save this draft. " +
            "Changing selection or recompiling scripts also discards the draft.",
            MessageType.Info);
        if (assetWasDirtyAtOpen)
        {
            EditorGUILayout.HelpBox(
                "This card already had unsaved changes before this Inspector " +
                "opened. Cancel keeps that pre-existing state.",
                MessageType.Warning);
        }

        EditorGUILayout.LabelField("Card Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(cardId, new GUIContent("Card ID"));

        if (string.IsNullOrWhiteSpace(cardId.stringValue))
        {
            EditorGUILayout.HelpBox(
                "Card ID is required for decks, saves, replays, and networking. " +
                "Use a stable value such as fire_dragon.",
                MessageType.Warning);
        }

        EditorGUILayout.PropertyField(category);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Common Information", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(cardName);
        EditorGUILayout.PropertyField(artwork);
        EditorGUILayout.PropertyField(effectText);
        DrawEffects();

        EditorGUILayout.Space();
        DrawCategoryFields((CardCategory)category.enumValueIndex);

        stagedObject.ApplyModifiedProperties();
        DrawSaveControls();
    }

    private void DrawSaveControls()
    {
        bool hasChanges = !string.Equals(
            EditorJsonUtility.ToJson(stagedCard), stagedBaseline,
            StringComparison.Ordinal);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            hasChanges ? "Unsaved Inspector changes" : "No pending Inspector changes",
            EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!hasChanges))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Card Changes"))
                SaveCardChanges();
            if (GUILayout.Button("Cancel Changes"))
                CancelChanges();
            EditorGUILayout.EndHorizontal();
        }
    }

    private void SaveCardChanges()
    {
        if (!string.Equals(
            EditorJsonUtility.ToJson(target), assetBaseline,
            StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog(
                "Card changed elsewhere",
                "The card asset changed outside this Inspector. Cancel this " +
                "draft and reopen it before editing to avoid overwriting " +
                "those changes.",
                "OK");
            return;
        }

        Undo.RecordObject(target, "Save Card Data Changes");
        EditorUtility.CopySerializedManagedFieldsOnly(stagedCard, target);
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssetIfDirty(target);
        assetBaseline = EditorJsonUtility.ToJson(target);
        stagedBaseline = EditorJsonUtility.ToJson(stagedCard);
        assetWasDirtyAtOpen = false;
    }

    private void CancelChanges()
    {
        EditorUtility.CopySerializedManagedFieldsOnly(target, stagedCard);
        stagedObject.Update();
        BindProperties();
        assetBaseline = EditorJsonUtility.ToJson(target);
        stagedBaseline = EditorJsonUtility.ToJson(stagedCard);
        assetWasDirtyAtOpen = EditorUtility.IsDirty(target);
    }

    private void DrawEffects()
    {
        EditorGUILayout.PropertyField(effects, new GUIContent("Effects"), false);
        if (!effects.isExpanded)
            return;

        EditorGUI.indentLevel++;
        for (int index = 0; index < effects.arraySize; index++)
        {
            SerializedProperty effect = effects.GetArrayElementAtIndex(index);
            EditorGUILayout.PropertyField(
                effect, new GUIContent("Effect " + index), false);
            if (!effect.isExpanded)
                continue;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(effect.FindPropertyRelative("effectId"));
            SerializedProperty kind = effect.FindPropertyRelative("effectKind");
            EditorGUILayout.PropertyField(kind, new GUIContent("Effect Kind"));
            bool continuous = kind.enumValueIndex == (int)EffectKind.Continuous;

            if (!continuous)
            {
                EditorGUILayout.PropertyField(effect.FindPropertyRelative("trigger"));
                EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
                SerializedProperty phases =
                    effect.FindPropertyRelative("allowedPhases");
                phases.intValue = (int)(EffectPhaseMask)EditorGUILayout.EnumFlagsField(
                    "Allowed Phases", (EffectPhaseMask)phases.intValue);
                EditorGUILayout.PropertyField(
                    effect.FindPropertyRelative("allowedTurn"),
                    new GUIContent("Allowed Turn"));
                EditorGUILayout.PropertyField(
                    effect.FindPropertyRelative("activationWindow"),
                    new GUIContent("Window"));
            }

            SerializedProperty conditions =
                effect.FindPropertyRelative("conditions");
            EditorGUILayout.PropertyField(conditions, true);
            WarnMissingComponents(conditions);

            if (continuous)
            {
                SerializedProperty modifiers =
                    effect.FindPropertyRelative("continuousModifiers");
                EditorGUILayout.PropertyField(modifiers,
                    new GUIContent("Continuous Modifiers"), true);
                WarnMissingComponents(modifiers);
            }
            else
            {
                EditorGUILayout.LabelField("Activation", EditorStyles.boldLabel);
                DrawQuantifiedList(effect.FindPropertyRelative("costs"));
                DrawQuantifiedList(effect.FindPropertyRelative("targets"));
                DrawQuantifiedList(effect.FindPropertyRelative("resolution"));

                EditorGUILayout.LabelField("Usage Limit", EditorStyles.boldLabel);
                SerializedProperty maxUses =
                    effect.FindPropertyRelative("maxUses");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    new GUIContent("Max Uses", "0 means unlimited."),
                    GUILayout.Width(100));
                maxUses.intValue = Mathf.Max(0,
                    EditorGUILayout.IntField(maxUses.intValue,
                        GUILayout.Width(64)));
                EditorGUILayout.LabelField("0 = unlimited",
                    EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(
                    effect.FindPropertyRelative("countBy"),
                    new GUIContent("Count By"));
                EditorGUILayout.PropertyField(
                    effect.FindPropertyRelative("reset"));
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", GUILayout.Width(28)))
        {
            int nextIndex = effects.arraySize;
            effects.arraySize++;
            SerializedProperty added = effects.GetArrayElementAtIndex(nextIndex);
            added.FindPropertyRelative("effectId").stringValue =
                cardId.stringValue + "_effect_" + nextIndex;
            added.FindPropertyRelative("effectKind").enumValueIndex =
                (int)EffectKind.Activated;
            added.FindPropertyRelative("trigger").enumValueIndex =
                (int)EffectTriggerType.ManualActivation;
            added.FindPropertyRelative("allowedPhases").intValue =
                (int)EffectPhaseMask.Main;
            added.FindPropertyRelative("allowedTurn").enumValueIndex =
                (int)EffectTurnScope.YourTurn;
            added.FindPropertyRelative("activationWindow").enumValueIndex =
                (int)EffectActivationWindow.OpenGameState;
            added.FindPropertyRelative("conditions").arraySize = 0;
            added.FindPropertyRelative("costs").arraySize = 0;
            added.FindPropertyRelative("targets").arraySize = 0;
            added.FindPropertyRelative("resolution").arraySize = 0;
            added.FindPropertyRelative("continuousModifiers").arraySize = 0;
            added.FindPropertyRelative("maxUses").intValue = 0;
            added.FindPropertyRelative("countBy").enumValueIndex =
                (int)EffectUsageScope.PerInstance;
            added.FindPropertyRelative("reset").enumValueIndex =
                (int)EffectUsageReset.PerTurn;
        }
        using (new EditorGUI.DisabledScope(effects.arraySize == 0))
        {
            if (GUILayout.Button("-", GUILayout.Width(28)))
                effects.arraySize--;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUI.indentLevel--;
    }

    private static void DrawQuantifiedList(SerializedProperty list)
    {
        int previousSize = list.arraySize;
        EditorGUILayout.PropertyField(list, true);

        // Unity's default list '+' creates an element with Amount = 0 even
        // though the serializable slot's field initializer is 1.
        for (int index = previousSize; index < list.arraySize; index++)
        {
            list.GetArrayElementAtIndex(index)
                .FindPropertyRelative("amount").intValue = 1;
        }

        for (int index = 0; index < list.arraySize; index++)
        {
            SerializedProperty amount = list.GetArrayElementAtIndex(index)
                .FindPropertyRelative("amount");
            if (amount.intValue < 1)
            {
                EditorGUILayout.HelpBox(
                    $"{list.displayName} element {index}: Amount must be at least 1.",
                    MessageType.Warning);
            }
        }
        WarnMissingComponents(list);
    }

    private static void WarnMissingComponents(SerializedProperty list)
    {
        for (int index = 0; index < list.arraySize; index++)
        {
            SerializedProperty component = list.GetArrayElementAtIndex(index)
                .FindPropertyRelative("component");
            if (component.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    $"{list.displayName} element {index} has no component. " +
                    "Assign one, or remove this element if it is not needed.",
                    MessageType.Warning);
            }
        }
    }

    private void DrawCategoryFields(CardCategory selectedCategory)
    {
        switch (selectedCategory)
        {
            case CardCategory.Monster:
                DrawMonsterFields();
                break;
            case CardCategory.Spell:
                DrawSpellFields();
                break;
            case CardCategory.Trap:
                DrawTrapFields();
                break;
        }
    }

    private void DrawMonsterFields()
    {
        EditorGUILayout.LabelField("Monster Information", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(monsterAttribute);
        EditorGUILayout.PropertyField(monsterRank);
        EditorGUILayout.PropertyField(monsterType);
        EditorGUILayout.PropertyField(baseHealth);
        EditorGUILayout.PropertyField(baseAttack);
        EditorGUILayout.PropertyField(baseDefense);
        EditorGUILayout.PropertyField(attackEnergyRequirement);

        DrawDerivedValue(
            "Tributes Required",
            GetTributeCount((MonsterRank)monsterRank.enumValueIndex).ToString());
        DrawDerivedValue("Activation Speed", CardSpeed.Speed1.ToString());
    }

    private void DrawSpellFields()
    {
        EditorGUILayout.LabelField("Spell Information", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(spellType);
        DrawDerivedValue("Activation Speed", CardSpeed.Speed1.ToString());
    }

    private void DrawTrapFields()
    {
        EditorGUILayout.LabelField("Trap Information", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trapType);

        CardSpeed speed = (TrapType)trapType.enumValueIndex == TrapType.Counter
            ? CardSpeed.Speed3
            : CardSpeed.Speed2;

        DrawDerivedValue("Activation Speed", speed.ToString());
    }

    private static void DrawDerivedValue(string label, string value)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField(label, value);
        }
    }

    private static int GetTributeCount(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Advanced:
                return 1;
            case MonsterRank.Supreme:
                return 2;
            case MonsterRank.Ultimate:
                return 3;
            default:
                return 0;
        }
    }
}
