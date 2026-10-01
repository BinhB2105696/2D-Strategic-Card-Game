using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardData))]
public class CardDataEditor : Editor
{
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
        cardId = serializedObject.FindProperty("cardId");
        cardName = serializedObject.FindProperty("cardName");
        category = serializedObject.FindProperty("category");
        artwork = serializedObject.FindProperty("artwork");
        effectText = serializedObject.FindProperty("effectText");
        monsterAttribute = serializedObject.FindProperty("monsterAttribute");
        monsterRank = serializedObject.FindProperty("monsterRank");
        monsterType = serializedObject.FindProperty("monsterType");
        baseHealth = serializedObject.FindProperty("baseHealth");
        baseAttack = serializedObject.FindProperty("baseAttack");
        baseDefense = serializedObject.FindProperty("baseDefense");
        attackEnergyRequirement =
            serializedObject.FindProperty("attackEnergyRequirement");
        spellType = serializedObject.FindProperty("spellType");
        trapType = serializedObject.FindProperty("trapType");
        effects = serializedObject.FindProperty("effects");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

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
        EditorGUILayout.PropertyField(
            effects,
            new GUIContent("Effect Data Assets"),
            true);

        EditorGUILayout.Space();
        DrawCategoryFields((CardCategory)category.enumValueIndex);

        serializedObject.ApplyModifiedProperties();
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
