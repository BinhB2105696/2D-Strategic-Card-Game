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
            CardEffectData effect = cardData.Effects[index];

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

    private static EffectDefinition ConvertEffect(CardEffectData effect)
    {
        List<EffectConditionDefinition> conditions =
            new List<EffectConditionDefinition>(effect.Conditions.Count);
        List<EffectCostDefinition> costs =
            new List<EffectCostDefinition>(effect.Costs.Count);
        List<EffectOperationDefinition> operations =
            new List<EffectOperationDefinition>(effect.Resolution.Count);

        for (int index = 0; index < effect.Conditions.Count; index++)
        {
            EffectConditionSlot condition = effect.Conditions[index];
            if (condition == null || condition.Component == null)
                throw new InvalidOperationException("Missing effect condition component.");
            conditions.Add(condition.Component.CreateDefinition(condition.Amount));
        }

        for (int index = 0; index < effect.Costs.Count; index++)
        {
            EffectCostSlot cost = effect.Costs[index];
            if (cost == null || cost.Component == null)
                throw new InvalidOperationException("Missing effect cost component.");
            costs.Add(cost.Component.CreateDefinition(cost.Amount));
        }

        for (int index = 0; index < effect.Resolution.Count; index++)
        {
            EffectResolutionSlot operation = effect.Resolution[index];
            if (operation == null || operation.Component == null)
                throw new InvalidOperationException("Missing effect resolution component.");
            operations.Add(operation.Component.CreateDefinition(operation.Amount));
        }

        if (effect.Targets.Count > 1)
            throw new InvalidOperationException(
                "Multiple target components are not supported yet.");
        EffectTargetType targetType = EffectTargetType.None;
        int targetCount = 0;
        if (effect.Targets.Count == 1)
        {
            EffectTargetSlot target = effect.Targets[0];
            if (target == null || target.Component == null)
                throw new InvalidOperationException("Missing effect target component.");
            if (target.Amount < 1)
                throw new InvalidOperationException("Effect target amount must be at least 1.");
            targetType = target.Component.CreateDefinition();
            targetCount = target.Amount;
        }

        List<IContinuousModifier> modifiers = new List<IContinuousModifier>();
        for (int index = 0; index < effect.ContinuousModifiers.Count; index++)
        {
            ContinuousModifierSlot slot = effect.ContinuousModifiers[index];
            if (slot == null || slot.Component == null)
                throw new InvalidOperationException("Missing continuous modifier component.");
            modifiers.Add(slot.Component.CreateModifier(slot.Amount));
        }

        return new EffectDefinition(
            effect.EffectId,
            effect.Trigger,
            effect.Kind,
            effect.AllowedPhases,
            effect.AllowedTurn,
            effect.ActivationWindow,
            conditions,
            costs,
            targetType,
            targetCount,
            operations,
            effect.MaxUses,
            effect.CountBy,
            effect.Reset,
            modifiers);
    }
}
