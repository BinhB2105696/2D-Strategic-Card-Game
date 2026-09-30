using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class CardPrefabSetupTool
{
    private const string CardBasePath = "Assets/Prefabs/Card_Base.prefab";
    private const string CardMonsterPath = "Assets/Prefabs/Card_Monster.prefab";
    private const string CardSpellTrapPath = "Assets/Prefabs/Card_SpellTrap.prefab";

    [MenuItem("Card Game/Setup Card Base Visual Layers")]
    public static void SetupCardBaseVisualLayers()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CardBasePath);

        try
        {
            CardView cardView = prefabRoot.GetComponent<CardView>();
            Image frameImage = prefabRoot.GetComponent<Image>();

            if (cardView == null || frameImage == null)
            {
                Debug.LogError(
                    "Card_Base requires CardView and Image components on its root.");
                return;
            }

            Image cardBackImage = GetOrCreateCardBack(prefabRoot.transform);
            SerializedObject cardViewObject = new SerializedObject(cardView);
            cardViewObject.FindProperty("cardFrameImage").objectReferenceValue = frameImage;
            cardViewObject.FindProperty("cardBackImage").objectReferenceValue = cardBackImage;

            cardViewObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cardView);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, CardBasePath);

        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        SetupMonsterReferences();
        SetupSpellTrapReferences();

        Debug.Log(
            "Card prefabs configured: Card_Back and all current UI references " +
            "were linked without changing their RectTransforms.");
    }

    private static Image GetOrCreateCardBack(Transform root)
    {
        Transform existing = root.Find("Card_Back");
        GameObject cardBackObject;

        if (existing != null)
        {
            cardBackObject = existing.gameObject;
        }
        else
        {
            cardBackObject = new GameObject(
                "Card_Back",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            cardBackObject.transform.SetParent(root, false);
        }

        RectTransform rectTransform = cardBackObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.SetAsLastSibling();

        Image image = cardBackObject.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = false;

        cardBackObject.SetActive(false);
        return image;
    }

    private static void SetupMonsterReferences()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CardMonsterPath);

        try
        {
            CardView cardView = prefabRoot.GetComponent<CardView>();
            SerializedObject viewObject = new SerializedObject(cardView);

            SetReference(viewObject, "attributeIconImage",
                FindChildComponent<Image>(prefabRoot.transform, "Monster_Attribute"));
            SetReference(viewObject, "effectText",
                FindChildComponent<TMP_Text>(prefabRoot.transform, "Monster_Effect"));
            SetReference(viewObject, "rankText",
                FindChildComponent<TMP_Text>(prefabRoot.transform, "Monster_Rank"));
            SetReference(viewObject, "healthText",
                FindChildComponent<TMP_Text>(prefabRoot.transform, "HP_Text"));
            SetReference(viewObject, "attackText",
                FindChildComponent<TMP_Text>(prefabRoot.transform, "ATK_Text"));
            SetReference(viewObject, "defenseText",
                FindChildComponent<TMP_Text>(prefabRoot.transform, "DEF_Text"));

            viewObject.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, CardMonsterPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void SetupSpellTrapReferences()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CardSpellTrapPath);

        try
        {
            CardView cardView = prefabRoot.GetComponent<CardView>();
            SerializedObject viewObject = new SerializedObject(cardView);

            SetReference(viewObject, "effectText",
                FindChildComponent<TMP_Text>(prefabRoot.transform, "SpellTrap_Effect"));

            viewObject.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, CardSpellTrapPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void SetReference(
        SerializedObject serializedObject,
        string propertyName,
        Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static T FindChildComponent<T>(Transform root, string childName)
        where T : Component
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child.GetComponent<T>();
            }
        }

        return null;
    }
}
