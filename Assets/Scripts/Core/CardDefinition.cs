using System;
using System.Collections.Generic;

[Serializable]
public sealed class CardDefinition
{
    private static readonly EffectDefinition[] NoEffects =
        new EffectDefinition[0];

    public string CardId { get; }
    public string CardName { get; }
    public CardCategory Category { get; }
    public string EffectText { get; }

    public MonsterAttribute MonsterAttribute { get; }
    public MonsterRank MonsterRank { get; }
    public MonsterType MonsterType { get; }
    public int BaseHealth { get; }
    public int BaseAttack { get; }
    public int BaseDefense { get; }
    public int AttackEnergyRequirement { get; }

    public SpellType SpellType { get; }
    public TrapType TrapType { get; }
    public int TributeCount { get; }
    public CardSpeed Speed { get; }
    public IReadOnlyList<EffectDefinition> Effects { get; }

    public bool IsMonster => Category == CardCategory.Monster;

    public CardDefinition(
        string cardId,
        string cardName,
        CardCategory category,
        string effectText,
        MonsterAttribute monsterAttribute,
        MonsterRank monsterRank,
        MonsterType monsterType,
        int baseHealth,
        int baseAttack,
        int baseDefense,
        int attackEnergyRequirement,
        SpellType spellType,
        TrapType trapType,
        int tributeCount,
        CardSpeed speed,
        IReadOnlyList<EffectDefinition> effects = null)
    {
        if (string.IsNullOrWhiteSpace(cardId))
        {
            throw new ArgumentException(
                "A card definition requires a stable Card ID.",
                nameof(cardId));
        }

        if (string.IsNullOrWhiteSpace(cardName))
        {
            throw new ArgumentException(
                "A card definition requires a Card Name.",
                nameof(cardName));
        }

        CardId = cardId;
        CardName = cardName;
        Category = category;
        EffectText = effectText ?? string.Empty;
        MonsterAttribute = monsterAttribute;
        MonsterRank = monsterRank;
        MonsterType = monsterType;
        BaseHealth = Math.Max(0, baseHealth);
        BaseAttack = Math.Max(0, baseAttack);
        BaseDefense = Math.Max(0, baseDefense);
        AttackEnergyRequirement = Math.Max(0, attackEnergyRequirement);
        SpellType = spellType;
        TrapType = trapType;
        TributeCount = Math.Max(0, tributeCount);
        Speed = speed;
        Effects = CopyEffects(effects);
    }

    private static IReadOnlyList<EffectDefinition> CopyEffects(
        IReadOnlyList<EffectDefinition> effects)
    {
        if (effects == null || effects.Count == 0)
        {
            return NoEffects;
        }

        EffectDefinition[] copy = new EffectDefinition[effects.Count];

        for (int index = 0; index < effects.Count; index++)
        {
            copy[index] = effects[index] ?? throw new ArgumentException(
                $"Effect definition at index {index} is null.",
                nameof(effects));
        }

        return copy;
    }
}
