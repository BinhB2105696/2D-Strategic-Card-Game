using UnityEngine;

[CreateAssetMenu(
    fileName = "Card Visual Library",
    menuName = "Card Game/Card Visual Library")]
public class CardVisualLibrary : ScriptableObject
{
    [Header("Card Frames")]
    [SerializeField] private Sprite monsterFrame;
    [SerializeField] private Sprite spellFrame;
    [SerializeField] private Sprite trapFrame;
    [SerializeField] private Sprite cardBack;

    [Header("Monster Attribute Icons")]
    [SerializeField] private Sprite metalIcon;
    [SerializeField] private Sprite woodIcon;
    [SerializeField] private Sprite fireIcon;
    [SerializeField] private Sprite waterIcon;
    [SerializeField] private Sprite earthIcon;
    [SerializeField] private Sprite windIcon;

    public Sprite CardBack => cardBack;

    public Sprite GetFrame(CardCategory category)
    {
        switch (category)
        {
            case CardCategory.Monster:
                return monsterFrame;
            case CardCategory.Spell:
                return spellFrame;
            case CardCategory.Trap:
                return trapFrame;
            default:
                return null;
        }
    }

    public Sprite GetMonsterAttributeIcon(MonsterAttribute attribute)
    {
        switch (attribute)
        {
            case MonsterAttribute.Metal:
                return metalIcon;
            case MonsterAttribute.Wood:
                return woodIcon;
            case MonsterAttribute.Fire:
                return fireIcon;
            case MonsterAttribute.Water:
                return waterIcon;
            case MonsterAttribute.Earth:
                return earthIcon;
            case MonsterAttribute.Wind:
                return windIcon;
            default:
                return null;
        }
    }
}
