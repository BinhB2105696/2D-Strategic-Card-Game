using System;

public static class CardDataConverter
{
    public static CardDefinition ToDefinition(CardData cardData)
    {
        if (cardData == null)
        {
            throw new ArgumentNullException(nameof(cardData));
        }

        return new CardDefinition(
            cardData.CardId,
            cardData.CardName,
            cardData.Category,
            cardData.EffectText,
            cardData.MonsterAttribute,
            cardData.MonsterRank,
            cardData.MonsterType,
            cardData.BaseHealth,
            cardData.BaseAttack,
            cardData.BaseDefense,
            cardData.AttackEnergyRequirement,
            cardData.SpellType,
            cardData.TrapType,
            cardData.EnergyCost,
            cardData.TributeCount,
            cardData.Speed);
    }
}
