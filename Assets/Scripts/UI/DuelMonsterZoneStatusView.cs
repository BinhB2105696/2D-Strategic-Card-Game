using TMPro;
using UnityEngine;

// Runtime combat information stays upright on the Monster Zone while the
// card itself may rotate into Defense Position.
public sealed class DuelMonsterZoneStatusView : MonoBehaviour
{
    private const string CombatFontName = "Cinzel-Bold SDF";
    private const int CombatStatusSortingOrder = 15;

    private static readonly Color ActiveStatColor =
        new Color32(255, 215, 76, 255);
    private static readonly Color InactiveStatColor =
        new Color32(225, 235, 245, 210);

    private TMP_Text healthText;
    private TMP_Text attackText;
    private TMP_Text defenseText;
    private TMP_Text energyText;
    private GameObject root;

    public void Initialize(TMP_FontAsset font)
    {
        if (root != null)
            return;

        TMP_FontAsset combatFont = ResolveCombatFont(font);

        root = new GameObject("RuntimeMonsterStatus", typeof(RectTransform));
        root.layer = gameObject.layer;
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.SetAsLastSibling();

        Canvas statusCanvas = root.AddComponent<Canvas>();
        statusCanvas.overrideSorting = true;
        statusCanvas.sortingOrder = CombatStatusSortingOrder;

        healthText = CreateLabel(
            rootRect,
            "HealthText",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(210f, 40f),
            new Vector2(0f, 25f),
            35f,
            5f,
            FontStyles.Normal,
            0.5f,
            0.5f,
            TextAlignmentOptions.Center,
            combatFont);
        healthText.color = new Color32(255, 235, 235, 255);

        energyText = CreateLabel(
            rootRect,
            "EnergyText",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0f, 0f),
            new Vector2(95f, 40f),
            new Vector2(-95f, -20f),
            35f,
            0f,
            FontStyles.Bold,
            0.25f,
            0.25f,
            TextAlignmentOptions.Center,
            combatFont);
        energyText.color = new Color32(92, 225, 230, 255);

        attackText = CreateLabel(
            rootRect,
            "AttackText",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 0f),
            new Vector2(210f, 40f),
            new Vector2(-105f, -15f),
            35f,
            5f,
            FontStyles.Normal,
            0.5f,
            0.5f,
            TextAlignmentOptions.Center,
            combatFont);

        defenseText = CreateLabel(
            rootRect,
            "DefenseText",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 0f),
            new Vector2(210f, 40f),
            new Vector2(-105f, -48f),
            35f,
            5f,
            FontStyles.Normal,
            0.5f,
            0.5f,
            TextAlignmentOptions.Center,
            combatFont);

        root.SetActive(false);
    }

    public void SetMonster(CardInstance monster)
    {
        if (root == null)
            return;

        bool visible = monster != null && monster.Definition.IsMonster;
        root.SetActive(visible);
        if (!visible)
            return;

        SetHealthText(healthText, monster.CurrentHealth, monster.MaximumHealth);
        attackText.text = "ATK " + monster.CurrentAttack;
        defenseText.text = "DEF " + monster.CurrentDefense;
        energyText.text = "E " + monster.AllocatedEnergy + "/" +
            monster.Definition.AttackEnergyRequirement;

        bool attackPosition =
            monster.BattlePosition == BattlePosition.Attack;
        attackText.color = attackPosition
            ? ActiveStatColor
            : InactiveStatColor;
        defenseText.color = attackPosition
            ? InactiveStatColor
            : ActiveStatColor;
        attackText.fontStyle = attackPosition
            ? FontStyles.Bold
            : FontStyles.Normal;
        defenseText.fontStyle = attackPosition
            ? FontStyles.Normal
            : FontStyles.Bold;
    }

    // Shared by the stationary HUD and its animation clone. Text only: preserve layout.
    public static void SetHealthText(TMP_Text label, int current, int maximum)
    {
        if (label == null) return;
        label.text = "HP " + current + "/" + maximum;
    }

    private static TMP_Text CreateLabel(
        RectTransform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 position,
        float fontSize,
        float characterSpacing,
        FontStyles fontStyle,
        float faceDilate,
        float outlineThickness,
        TextAlignmentOptions alignment,
        TMP_FontAsset font)
    {
        GameObject labelObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.layer = parent.gameObject.layer;
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.fontStyle = fontStyle;
        text.characterSpacing = characterSpacing;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, faceDilate);
        text.outlineWidth = outlineThickness;
        text.outlineColor = new Color32(0, 0, 0, 220);
        return text;
    }

    private static TMP_FontAsset ResolveCombatFont(TMP_FontAsset fallback)
    {
        TMP_FontAsset[] loadedFonts =
            Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (TMP_FontAsset loadedFont in loadedFonts)
        {
            if (loadedFont != null && loadedFont.name == CombatFontName)
                return loadedFont;
        }

        return fallback;
    }
}
