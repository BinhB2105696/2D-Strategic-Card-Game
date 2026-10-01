using System;
using System.Collections.Generic;

public static class CardDataConverter
{
    public static CardDefinition ToDefinition(CardData cardData)
    {
        if (cardData == null)
        {
            throw new ArgumentNullException(nameof(cardData));
        }

        List<EffectDefinition> effects =
            new List<EffectDefinition>(cardData.Effects.Count);

        for (int index = 0; index < cardData.Effects.Count; index++)
        {
            EffectData effect = cardData.Effects[index];

            if (effect != null)
            {
                effects.Add(ConvertEffect(effect));
            }
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
            cardData.TributeCount,
            cardData.Speed,
            effects);
    }

    private static EffectDefinition ConvertEffect(EffectData effect)
    {
        List<EffectConditionDefinition> conditions =
            new List<EffectConditionDefinition>(effect.Conditions.Count);
        List<EffectCostDefinition> costs =
            new List<EffectCostDefinition>(effect.Costs.Count);
        List<EffectOperationDefinition> operations =
            new List<EffectOperationDefinition>(effect.Operations.Count);

        for (int index = 0; index < effect.Conditions.Count; index++)
        {
            EffectConditionData condition = effect.Conditions[index];

            if (condition != null)
            {
                conditions.Add(new EffectConditionDefinition(
                    condition.Type,
                    condition.Amount));
            }
        }

        for (int index = 0; index < effect.Costs.Count; index++)
        {
            EffectCostData cost = effect.Costs[index];

            if (cost != null)
            {
                costs.Add(new EffectCostDefinition(
                    cost.Type,
                    cost.Amount));
            }
        }

        for (int index = 0; index < effect.Operations.Count; index++)
        {
            EffectOperationData operation = effect.Operations[index];

            if (operation != null)
            {
                operations.Add(new EffectOperationDefinition(
                    operation.Type,
                    operation.Amount));
            }
        }

        return new EffectDefinition(
            effect.EffectId,
            effect.Trigger,
            effect.ActivationPhase,
            conditions,
            costs,
            effect.TargetType,
            operations,
            effect.MaxUsesPerTurn);
    }
}
