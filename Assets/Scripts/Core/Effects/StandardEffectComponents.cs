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
        if (context.TargetInstanceIds.Count != effect.TargetCount)
        {
            failureReason =
                "This effect requires exactly " + effect.TargetCount +
                " target(s).";
            return false;
        }

        if (effect.TargetType == EffectTargetType.None)
        {
            failureReason = string.Empty;
            return effect.TargetCount == 0;
        }

        HashSet<long> uniqueTargets = new HashSet<long>();
        for (int index = 0; index < context.TargetInstanceIds.Count; index++)
        {
            long targetId = context.TargetInstanceIds[index];
            if (!uniqueTargets.Add(targetId))
            {
                failureReason = "The same card cannot be selected more than once.";
                return false;
            }
            int ownerIndex = effect.TargetType == EffectTargetType.OwnMonster
                ? context.ActivatingPlayerIndex
                : 1 - context.ActivatingPlayerIndex;
            if (context.State.Players[ownerIndex]
                    .FindMonsterZoneIndex(targetId) < 0)
            {
                failureReason = effect.TargetType == EffectTargetType.OwnMonster
                    ? "The target must be one of your field Monsters."
                    : "The target must be an opponent field Monster.";
                return false;
            }
        }

        failureReason = string.Empty;
        return true;
    }
}

public sealed class DefinitionCardEffect : ICardEffect
{
    // Reusable resolution dispatch for authored card effects.
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

                case EffectOperationType.SkipNextOpponentDrawStep:
                    int opponentIndex = 1 - context.ActivatingPlayerIndex;
                    int drawTurn = context.State.GetNextDrawTurnForPlayer(
                        opponentIndex);
                    context.Opponent.ScheduleDrawStepSkip(drawTurn);
                    break;

                case EffectOperationType.ReduceAttackUntilEndOfTurn:
                    ResolveTemporaryAttackReduction(
                        context,
                        operation.Amount,
                        events);
                    break;

                case EffectOperationType.IncreaseAttackUntilEndOfTurn:
                    ResolveTemporaryStatIncrease(
                        context,
                        operation.Amount,
                        0,
                        events);
                    break;

                case EffectOperationType.IncreaseDefenseUntilEndOfTurn:
                    ResolveTemporaryStatIncrease(
                        context,
                        0,
                        operation.Amount,
                        events);
                    break;

                case EffectOperationType.RestoreMonsterHealth:
                    ResolveHealthRestore(context, operation.Amount, events);
                    break;

                case EffectOperationType.NegatePreviousChainLink:
                    ResolveNegation(context, events);
                    break;

                case EffectOperationType.IncreaseDefensePermanently:
                    ResolvePermanentDefenseIncrease(
                        context,
                        operation.Amount,
                        events);
                    break;

                case EffectOperationType.IncreaseAttackByTriggerValue:
                    ResolvePermanentAttackIncrease(
                        context,
                        context.TriggerValue,
                        events);
                    break;

                case EffectOperationType.RestoreHealthPerRemovedAllocatedEnergy:
                    ResolveEnergyConversion(
                        context,
                        operation.Amount,
                        events);
                    break;

                case EffectOperationType.ProtectAttackedMonster:
                    ResolveAttackProtection(
                        context,
                        operation.Amount,
                        events);
                    break;

                case EffectOperationType.TransferAttackUntilEndOfTurn:
                    ResolveAttackTransfer(context, events);
                    break;

                case EffectOperationType.SkipCurrentBattlePhase:
                    context.ActivatingPlayer.ScheduleBattlePhaseSkip(
                        context.State.TurnNumber);
                    break;
            }
        }

        events.Add(GameEvent.EffectResolved(
            context.ActivatingPlayerIndex,
            context.SourceInstanceId,
            context.State.CurrentPhase));
    }

    private static void ResolvePermanentDefenseIncrease(
        EffectContext context,
        int amount,
        List<GameEvent> events)
    {
        CardInstance target = FindEffectMonster(context);
        if (target == null)
            return;

        int increase = System.Math.Max(0, amount);
        context.State.ApplyStatModifier(
            context.SourceInstanceId,
            target,
            0,
            increase);
        events.Add(GameEvent.MonsterDefenseModified(
            target.OwnerPlayerIndex,
            target.InstanceId,
            increase,
            context.State.CurrentPhase));
    }

    private static void ResolvePermanentAttackIncrease(
        EffectContext context,
        int amount,
        List<GameEvent> events)
    {
        CardInstance target = FindEffectMonster(context);
        if (target == null)
            return;

        int increase = System.Math.Max(0, amount);
        context.State.ApplyStatModifier(
            context.SourceInstanceId,
            target,
            increase,
            0);
        events.Add(GameEvent.MonsterAttackModified(
            target.OwnerPlayerIndex,
            target.InstanceId,
            increase,
            context.State.CurrentPhase));
    }

    private static void ResolveEnergyConversion(
        EffectContext context,
        int healthPerEnergy,
        List<GameEvent> events)
    {
        CardInstance target = FindEffectMonster(context);
        if (target == null)
            return;

        int removed = target.RemoveAllocatedEnergy(target.AllocatedEnergy);
        if (removed <= 0)
            return;

        events.Add(GameEvent.AllocatedEnergyRemoved(
            target.OwnerPlayerIndex,
            target.InstanceId,
            removed,
            context.State.CurrentPhase));
        int restored = removed * System.Math.Max(0, healthPerEnergy);
        target.RestoreHealth(restored);
        events.Add(GameEvent.MonsterHealthModified(
            target.OwnerPlayerIndex,
            target.InstanceId,
            restored,
            context.State.CurrentPhase));
    }

    private static void ResolveAttackProtection(
        EffectContext context,
        int defenseIncrease,
        List<GameEvent> events)
    {
        CardInstance target = FindEffectMonster(context);
        if (target == null)
            return;

        int increase = System.Math.Max(0, defenseIncrease);
        context.State.ApplyStatModifier(
            target.InstanceId,
            target,
            0,
            increase);
        events.Add(GameEvent.MonsterDefenseModified(
            target.OwnerPlayerIndex,
            target.InstanceId,
            increase,
            context.State.CurrentPhase));

        if (target.BattlePosition == BattlePosition.Attack)
        {
            target.SetBattlePosition(BattlePosition.Defense);
            events.Add(GameEvent.BattlePositionChanged(
                target.OwnerPlayerIndex,
                target.InstanceId,
                BattlePosition.Defense));

            int removed = target.RemoveAllocatedEnergy(target.AllocatedEnergy);
            if (removed > 0)
            {
                events.Add(GameEvent.AllocatedEnergyRemoved(
                    target.OwnerPlayerIndex,
                    target.InstanceId,
                    removed,
                    context.State.CurrentPhase));
            }
        }
    }

    private static void ResolveAttackTransfer(
        EffectContext context,
        List<GameEvent> events)
    {
        if (context.TargetInstanceIds.Count != 2)
            return;

        CardInstance donor = context.State.FindCard(
            context.TargetInstanceIds[0]);
        CardInstance receiver = context.State.FindCard(
            context.TargetInstanceIds[1]);
        if (donor == null || receiver == null ||
            donor.Zone != CardZone.MonsterZone ||
            receiver.Zone != CardZone.MonsterZone)
        {
            return;
        }

        int transferred = donor.CurrentAttack;
        if (transferred <= 0)
            return;

        context.State.ApplyStatModifier(
            context.SourceInstanceId,
            donor,
            -transferred,
            0,
            context.State.TurnNumber);
        context.State.ApplyStatModifier(
            context.SourceInstanceId,
            receiver,
            transferred,
            0,
            context.State.TurnNumber);
        events.Add(GameEvent.MonsterAttackModified(
            donor.OwnerPlayerIndex,
            donor.InstanceId,
            -transferred,
            context.State.CurrentPhase));
        events.Add(GameEvent.MonsterAttackModified(
            receiver.OwnerPlayerIndex,
            receiver.InstanceId,
            transferred,
            context.State.CurrentPhase));
    }

    private static void ResolveTemporaryAttackReduction(
        EffectContext context,
        int amount,
        List<GameEvent> events)
    {
        if (context.TargetInstanceIds.Count == 0)
            return;

        CardInstance target = context.State.FindCard(
            context.TargetInstanceIds[0]);
        if (target == null || target.Zone != CardZone.MonsterZone)
            return;

        int reduction = System.Math.Max(0, amount);
        context.State.ApplyStatModifier(
            context.SourceInstanceId,
            target,
            -reduction,
            0,
            context.State.TurnNumber);
        events.Add(GameEvent.MonsterAttackModified(
            target.OwnerPlayerIndex,
            target.InstanceId,
            -reduction,
            context.State.CurrentPhase));
    }

    private static void ResolveNegation(
        EffectContext context,
        List<GameEvent> events)
    {
        ChainState chain = context.State.ActiveChain;
        if (chain == null ||
            !chain.TryNegatePreviousLink(out ChainLink negatedLink))
        {
            return;
        }

        events.Add(GameEvent.CardActivationNegated(
            negatedLink.PlayerIndex,
            negatedLink.SourceInstanceId,
            context.State.CurrentPhase));
    }

    private static void ResolveTemporaryStatIncrease(
        EffectContext context,
        int attackAmount,
        int defenseAmount,
        List<GameEvent> events)
    {
        CardInstance target = FindEffectMonster(context);
        if (target == null)
            return;

        context.State.ApplyStatModifier(
            context.SourceInstanceId,
            target,
            System.Math.Max(0, attackAmount),
            System.Math.Max(0, defenseAmount),
            context.State.TurnNumber);
        if (attackAmount > 0)
        {
            events.Add(GameEvent.MonsterAttackModified(
                target.OwnerPlayerIndex,
                target.InstanceId,
                attackAmount,
                context.State.CurrentPhase));
        }
        if (defenseAmount > 0)
        {
            events.Add(GameEvent.MonsterDefenseModified(
                target.OwnerPlayerIndex,
                target.InstanceId,
                defenseAmount,
                context.State.CurrentPhase));
        }
    }

    private static void ResolveHealthRestore(
        EffectContext context,
        int amount,
        List<GameEvent> events)
    {
        CardInstance target = FindEffectMonster(context);
        if (target == null)
            return;

        int restored = System.Math.Max(0, amount);
        target.RestoreHealth(restored);
        events.Add(GameEvent.MonsterHealthModified(
            target.OwnerPlayerIndex,
            target.InstanceId,
            restored,
            context.State.CurrentPhase));
    }

    private static CardInstance FindEffectMonster(EffectContext context)
    {
        long targetId = context.TargetInstanceIds.Count > 0
            ? context.TargetInstanceIds[0]
            : context.SourceInstanceId;
        CardInstance target = context.State.FindCard(targetId);
        return target != null && target.Zone == CardZone.MonsterZone
            ? target
            : null;
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
