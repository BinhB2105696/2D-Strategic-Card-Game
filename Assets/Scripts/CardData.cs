using System.Collections.Generic;
using UnityEngine;

public enum CardCategory
{
    Monster,
    Spell,
    Trap
}

public enum MonsterAttribute
{
    Metal,
    Wood,
    Fire,
    Water,
    Earth,
    Wind
}

public enum MonsterRank
{
    Basic,
    Advanced,
    Supreme,
    Ultimate
}

public enum MonsterType
{
    Dragon,
    Warrior,
    Spellcaster,
    Fairy,
    Machine,
    Fiend,
    Animal,
    Insect,
    Plant,
    Spirit,
    Elf,
    Undead
}

public enum SpellType
{
    Normal,
    Equip,
    Field
}

public enum TrapType
{
    Normal,
    Counter
}

public enum CardSpeed
{
    Speed1 = 1,
    Speed2 = 2,
    Speed3 = 3
}

[CreateAssetMenu(fileName = "New Card", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    public const int MaxBaseStat = 5000;

    [Header("Basic Information")]
    [SerializeField] private string cardId;
    [SerializeField] private string cardName;
    [SerializeField] private CardCategory category;
    [SerializeField] private Sprite artwork;

    [TextArea(3, 8)]
    [SerializeField] private string effectText;

    [Header("Monster Information")]
    [SerializeField] private MonsterAttribute monsterAttribute;
    [SerializeField] private MonsterRank monsterRank;
    [SerializeField] private MonsterType monsterType;
    [Range(0, MaxBaseStat)] [SerializeField] private int baseHealth;
    [Range(0, MaxBaseStat)] [SerializeField] private int baseAttack;
    [Range(0, MaxBaseStat)] [SerializeField] private int baseDefense;
    [Min(0)] [SerializeField] private int attackEnergyRequirement;

    [Header("Spell Information")]
    [SerializeField] private SpellType spellType;

    [Header("Trap Information")]
    [SerializeField] private TrapType trapType;

    [Header("Effect References")]
    [SerializeField] private List<EffectData> effects =
        new List<EffectData>();

    public string CardId => cardId;
    public string CardName => cardName;
    public CardCategory Category => category;
    public Sprite Artwork => artwork;
    public string EffectText => effectText;

    public MonsterAttribute MonsterAttribute => monsterAttribute;
    public MonsterRank MonsterRank => monsterRank;
    public MonsterType MonsterType => monsterType;
    public int BaseHealth => baseHealth;
    public int BaseAttack => baseAttack;
    public int BaseDefense => baseDefense;
    public int AttackEnergyRequirement => attackEnergyRequirement;

    public SpellType SpellType => spellType;
    public TrapType TrapType => trapType;

    public IReadOnlyList<EffectData> Effects => effects;

    public int TributeCount
    {
        get
        {
            switch (monsterRank)
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

    public CardSpeed Speed
    {
        get
        {
            if (category == CardCategory.Trap)
            {
                return trapType == TrapType.Counter
                    ? CardSpeed.Speed3
                    : CardSpeed.Speed2;
            }

            return CardSpeed.Speed1;
        }
    }

    private void OnValidate()
    {
        baseHealth = Mathf.Clamp(baseHealth, 0, MaxBaseStat);
        baseAttack = Mathf.Clamp(baseAttack, 0, MaxBaseStat);
        baseDefense = Mathf.Clamp(baseDefense, 0, MaxBaseStat);
        attackEnergyRequirement = Mathf.Max(0, attackEnergyRequirement);
        if (effects == null)
        {
            effects = new List<EffectData>();
        }
    }
}
