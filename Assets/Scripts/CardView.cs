using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardView : MonoBehaviour
{
    [Header("Card Data")]
    [SerializeField] private CardData cardData;
    [SerializeField] private CardVisualLibrary visualLibrary;

    [Header("Common UI")]
    [SerializeField] private Image cardFrameImage;
    [SerializeField] private TMP_Text cardNameText;
    [SerializeField] private TMP_Text cardTypeText;
    [SerializeField] private Image artworkImage;
    [SerializeField] private Image attributeIconImage;
    [SerializeField] private TMP_Text effectText;
    [SerializeField] private Image cardBackImage;

    [Header("Card State")]
    [SerializeField] private bool startFaceDown;

    [Header("Monster UI")]
    [SerializeField] private GameObject monsterStatsContainer;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text defenseText;

    private void Start()
    {
        Refresh();
        SetFaceDown(startFaceDown);
    }

    public void SetCardData(CardData newCardData)
    {
        cardData = newCardData;
        Refresh();
    }

    [ContextMenu("Refresh Card Preview")]
    public void Refresh()
    {
        if (cardData == null)
        {
            Debug.LogWarning("CardView does not have CardData assigned.", this);
            return;
        }

        SetCommonInformation();
        SetCategoryInformation();
        SetCardVisuals();
    }

    private void SetCardVisuals()
    {
        if (visualLibrary == null)
        {
            return;
        }

        if (cardFrameImage != null)
        {
            cardFrameImage.sprite = visualLibrary.GetFrame(cardData.Category);
        }

        if (cardBackImage != null)
        {
            cardBackImage.sprite = visualLibrary.CardBack;
        }

        if (attributeIconImage == null)
        {
            return;
        }

        bool showsMonsterAttribute = cardData.Category == CardCategory.Monster;
        attributeIconImage.gameObject.SetActive(showsMonsterAttribute);

        if (showsMonsterAttribute)
        {
            attributeIconImage.sprite =
                visualLibrary.GetMonsterAttributeIcon(cardData.MonsterAttribute);
        }
    }

    public void SetFaceDown(bool faceDown)
    {
        // Keep the requested state when Start runs after a runtime card is spawned.
        startFaceDown = faceDown;

        if (cardBackImage == null)
        {
            Debug.LogWarning(
                "CardView does not have a Card Back Image assigned.",
                this);
            return;
        }

        SetFrontVisibility(!faceDown);
        cardBackImage.gameObject.SetActive(faceDown);
    }

    private void SetCommonInformation()
    {
        if (cardNameText != null)
        {
            cardNameText.text = cardData.CardName;
        }

        if (artworkImage != null)
        {
            artworkImage.sprite = cardData.Artwork;
            artworkImage.enabled = cardData.Artwork != null;
        }

        if (effectText != null)
        {
            effectText.text = cardData.EffectText;
        }
    }

    private void SetCategoryInformation()
    {
        bool isMonster = cardData.Category == CardCategory.Monster;
        SetMonsterStatsVisibility(isMonster);

        SetTextActive(rankText, isMonster);

        if (rankText != null && isMonster)
        {
            rankText.enableWordWrapping = false;
            rankText.text = $"<{cardData.MonsterRank}>";
        }

        if (cardTypeText != null)
        {
            cardTypeText.enableWordWrapping = false;
            cardTypeText.text = GetCardTypeName();
        }

        if (!isMonster)
        {
            return;
        }

        if (healthText == null || attackText == null || defenseText == null)
        {
            Debug.LogWarning(
                $"Monster card '{cardData.CardName}' is assigned to a CardView " +
                "without complete HP/ATK/DEF UI references. Use the Monster layout.",
                this);
            return;
        }

        healthText.text = cardData.BaseHealth.ToString();
        attackText.text = cardData.BaseAttack.ToString();
        defenseText.text = cardData.BaseDefense.ToString();
    }

    private void SetMonsterStatsVisibility(bool isVisible)
    {
        if (monsterStatsContainer != null)
        {
            monsterStatsContainer.SetActive(isVisible);
            return;
        }

        SetTextActive(healthText, isVisible);
        SetTextActive(attackText, isVisible);
        SetTextActive(defenseText, isVisible);
    }

    private static void SetTextActive(TMP_Text text, bool isActive)
    {
        if (text != null)
        {
            text.gameObject.SetActive(isActive);
        }
    }

    private static void SetImageActive(Image image, bool isActive)
    {
        if (image != null)
        {
            image.gameObject.SetActive(isActive);
        }
    }

    private void SetFrontVisibility(bool isVisible)
    {
        SetTextActive(cardNameText, isVisible);
        SetTextActive(cardTypeText, isVisible);
        SetImageActive(artworkImage, isVisible);
        SetTextActive(effectText, isVisible);

        bool showsMonsterInformation =
            isVisible &&
            cardData != null &&
            cardData.Category == CardCategory.Monster;

        SetImageActive(attributeIconImage, showsMonsterInformation);
        SetTextActive(rankText, showsMonsterInformation);
        SetMonsterStatsVisibility(showsMonsterInformation);
    }

    private string GetCardTypeName()
    {
        switch (cardData.Category)
        {
            case CardCategory.Monster:
                return $"[{cardData.MonsterType}]";

            case CardCategory.Spell:
                return GetSpellTypeName();

            case CardCategory.Trap:
                return GetTrapTypeName();

            default:
                return cardData.Category.ToString();
        }
    }

    private string GetSpellTypeName()
    {
        switch (cardData.SpellType)
        {
            case SpellType.Equip:
                return "[Equip Spell]";
            case SpellType.Field:
                return "[Field Spell]";
            default:
                return "[Normal Spell]";
        }
    }

    private string GetTrapTypeName()
    {
        return cardData.TrapType == TrapType.Counter
            ? "[Counter Trap]"
            : "[Normal Trap]";
    }
}
