using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EffectData))]
public sealed class EffectDataEditor : Editor
{
    private SerializedProperty effectId;
    private SerializedProperty trigger;
    private SerializedProperty activationPhase;
    private SerializedProperty maxUsesPerTurn;
    private SerializedProperty conditions;
    private SerializedProperty costs;
    private SerializedProperty targetType;
    private SerializedProperty operations;

    private void OnEnable()
    {
        effectId = serializedObject.FindProperty("effectId");
        trigger = serializedObject.FindProperty("trigger");
        activationPhase = serializedObject.FindProperty("activationPhase");
        maxUsesPerTurn = serializedObject.FindProperty("maxUsesPerTurn");
        conditions = serializedObject.FindProperty("conditions");
        costs = serializedObject.FindProperty("costs");
        targetType = serializedObject.FindProperty("targetType");
        operations = serializedObject.FindProperty("operations");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Effect Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(effectId, new GUIContent("Effect ID"));

        if (string.IsNullOrWhiteSpace(effectId.stringValue))
        {
            EditorGUILayout.HelpBox(
                "Effect ID is required for saves, replays, and networking.",
                MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Activation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trigger);
        EditorGUILayout.PropertyField(activationPhase);
        EditorGUILayout.PropertyField(
            maxUsesPerTurn,
            new GUIContent("Max Uses Per Turn (0 = Unlimited)"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Requirements", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(conditions, true);
        EditorGUILayout.PropertyField(costs, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Target and Resolution",
            EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(targetType);
        EditorGUILayout.PropertyField(operations, true);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Currently playable: Manual Activation in Main Phase, " +
            "Available Energy cost, no target, and Draw Cards operation. " +
            "Other options are reserved for later implementation.",
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }
}
