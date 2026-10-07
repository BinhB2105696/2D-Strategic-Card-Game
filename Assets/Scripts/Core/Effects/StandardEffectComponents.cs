using System.Collections.Generic;

public sealed class DefinitionEffectCondition : IEffectCondition
{
    private readonly EffectDefinition effect;

    public DefinitionEffectCondition(EffectDefinition effect)
    {
        this.effect = effect;
    }

    public bool IsMet(
        EffectContext context,
        out string failureReason)
    {
        for (int index = 0; index < effect.Conditions.Count; index++)
        {
            if (effect.Conditions[index].Type != EffectConditionType.None)
            {
                failureReason =
                    "This effect condition is not implemented yet.";
                return false;
            }
        }

        failureReason = string.Empty;
        return true;
    }
}

public sealed class DefinitionEffectCost : IEffectCost
{
    private readonly EffectDefinition effect;

    public DefinitionEffectCost(EffectDefinition effect)
    {
        this.effect = effect;
    }

    public bool CanPay(
        EffectContext context,
        out string failureReason)
    {
        int energyCost = 0;

        for (int index = 0; index < effect.Costs.Count; index++)
        {
            EffectCostDefinition cost = effect.Costs[index];

            if (cost.Type != EffectCostType.AvailableEnergy)
            {
                failureReason =
                    "This effect cost type is not implemented yet.";
                return false;
            }

            energyCost += cost.Amount;
        }

        bool canPay = context.ActivatingPlayer.AvailableEnergy >= energyCost;
        failureReason = canPay
            ? string.Empty
            : "Not enough Available Energy to activate this card.";
        return canPay;
    }

    public void Pay(
        EffectContext context,
        List<GameEvent> events)
    {
        int energyCost = 0;

        for (int index = 0; index < effect.Costs.Count; index++)
        {
            energyCost += effect.Costs[index].Amount;
        }

        context.ActivatingPlayer.TrySpendEnergy(energyCost);

        if (energyCost > 0)
        {
            events.Add(GameEvent.EnergySpent(
                context.ActivatingPlayerIndex,
                energyCost,
                context.State.CurrentPhase));
        }
    }
}

public sealed class EffectDefinitionTargetRule : IEffectTargetRule
{
    private readonly EffectDefinition effect;

    public EffectDefinitionTargetRule(EffectDefinition effect)
    {
        this.effect = effect;
    }

    public bool AreTargetsValid(
        EffectContext context,
        out string failureReason)
    {
        if (effect.TargetType != EffectTargetType.None ||
            effect.TargetCount != 0)
        {
            failureReason =
                "This target type is not implemented in the first " +
                "Normal Spell slice yet.";
            return false;
        }

        bool hasNoTargets = context.TargetInstanceIds.Count == 0;
        failureReason = hasNoTargets
            ? string.Empty
            : "This Spell does not require a target.";
        return hasNoTargets;
    }
}

public sealed class DefinitionCardEffect : ICardEffect
{
    private readonly EffectDefinition effect;

    public DefinitionCardEffect(EffectDefinition effect)
    {
        this.effect = effect;
    }

    public void Resolve(
        EffectContext context,
        List<GameEvent> events)
    {
        for (int index = 0; index < effect.Operations.Count; index++)
        {
            EffectOperationDefinition operation = effect.Operations[index];

            switch (operation.Type)
            {
                case EffectOperationType.DrawCards:
                    ResolveDrawCards(context, operation.Amount, events);
                    break;
            }
        }

        events.Add(GameEvent.EffectResolved(
            context.ActivatingPlayerIndex,
            context.SourceInstanceId,
            context.State.CurrentPhase));
    }

    private static void ResolveDrawCards(
        EffectContext context,
        int amount,
        List<GameEvent> events)
    {
        PlayerState player = context.ActivatingPlayer;
        int previousHandCount = player.Hand.Count;
        player.DrawCards(amount);

        for (int index = previousHandCount; index < player.Hand.Count; index++)
        {
            events.Add(GameEvent.CardDrawn(
                player.PlayerIndex,
                player.Hand[index].InstanceId,
                context.State.CurrentPhase));
        }
    }
}
