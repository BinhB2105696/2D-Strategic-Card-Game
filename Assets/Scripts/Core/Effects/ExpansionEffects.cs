using System;
using System.Collections.Generic;
using System.Linq;

public static class ExpansionEffects
{
    public static int EnergyCost(EffectDefinition effect, EffectContext context)
    {
        int total = 0;
        foreach (EffectCostDefinition cost in effect.Costs)
            if (cost.Type == EffectCostType.AvailableEnergy ||
                (cost.Type == EffectCostType.EnergyWithLpDeficitDiscount &&
                 context.State.LpDeficitAtMainStart(context.ActivatingPlayerIndex) < cost.SecondaryAmount))
                total += cost.Amount;
        return total;
    }

    public static bool CanPay(EffectDefinition effect, EffectContext context, out string reason)
    {
        reason = "Not enough resources to activate this effect.";
        int allocated = 0, discarded = 0;
        foreach (EffectCostDefinition cost in effect.Costs)
        {
            if (cost.Type == EffectCostType.RemoveAllocatedEnergy) allocated += cost.Amount;
            else if (cost.Type == EffectCostType.DiscardFromHand) discarded += cost.Amount;
            else if (cost.Type != EffectCostType.AvailableEnergy && cost.Type != EffectCostType.EnergyWithLpDeficitDiscount)
            { reason = "This effect cost type is not implemented yet."; return false; }
        }
        if (context.ActivatingPlayer.AvailableEnergy < EnergyCost(effect, context)) return false;
        CardInstance source = context.State.FindCard(context.SourceInstanceId);
        if (allocated > 0 && (source == null || source.Zone != CardZone.MonsterZone || source.AllocatedEnergy < allocated)) return false;
        if (discarded > context.TargetInstanceIds.Count) return false;
        var selected = new HashSet<long>();
        for (int index = 0; index < discarded; index++)
        {
            long id = context.TargetInstanceIds[index];
            CardInstance card = context.ActivatingPlayer.FindCardInHand(id);
            if (card == null || id == context.SourceInstanceId || !selected.Add(id)) return false;
        }
        reason = string.Empty;
        return true;
    }

    public static void Pay(EffectDefinition effect, EffectContext context, List<GameEvent> events)
    {
        int energy = EnergyCost(effect, context), discardIndex = 0;
        context.ActivatingPlayer.TrySpendEnergy(energy);
        if (energy > 0) events.Add(GameEvent.EnergySpent(context.ActivatingPlayerIndex, energy, context.State.CurrentPhase));
        foreach (EffectCostDefinition cost in effect.Costs)
        {
            if (cost.Type == EffectCostType.RemoveAllocatedEnergy)
            {
                CardInstance source = context.State.FindCard(context.SourceInstanceId);
                int removed = source.RemoveAllocatedEnergy(cost.Amount);
                events.Add(GameEvent.AllocatedEnergyRemoved(source.OwnerPlayerIndex, source.InstanceId, removed, context.State.CurrentPhase));
            }
            else if (cost.Type == EffectCostType.DiscardFromHand)
                for (int index = 0; index < cost.Amount; index++)
                {
                    CardInstance card = context.ActivatingPlayer.FindCardInHand(context.TargetInstanceIds[discardIndex++]);
                    context.ActivatingPlayer.MoveHandCardToGraveyard(card);
                    events.Add(GameEvent.CardSentToGraveyard(card.OwnerPlayerIndex, card.InstanceId, context.State.CurrentPhase));
                }
        }
    }

    public static bool Resolve(EffectOperationDefinition operation, EffectContext context, List<GameEvent> events)
    {
        GameState state = context.State;
        CardInstance target = context.TargetInstanceIds.Count > 0 ? state.FindCard(context.TargetInstanceIds[0]) : null;
        switch (operation.Type)
        {
            case EffectOperationType.RestoreAvailableEnergy:
                RestoreEnergy(context, operation.Amount, events); return true;
            case EffectOperationType.RestoreEnergyWithLpBonus:
                RestoreEnergy(context, operation.Amount + (context.ActivatingPlayer.Health < context.Opponent.Health ? operation.SecondaryAmount : 0), events); return true;
            case EffectOperationType.ReallocateEnergyToPlayer:
                if (target != null && target.Zone == CardZone.MonsterZone)
                {
                    int removed = target.RemoveAllocatedEnergy(operation.Amount);
                    events.Add(GameEvent.AllocatedEnergyRemoved(target.OwnerPlayerIndex, target.InstanceId, removed, state.CurrentPhase));
                    RestoreEnergy(context, removed, events);
                }
                return true;
            case EffectOperationType.SearchSelectedCard:
                if (target != null && target.OwnerPlayerIndex == context.ActivatingPlayerIndex && target.Zone == CardZone.Deck)
                {
                    context.ActivatingPlayer.MoveDeckCardToHand(target.InstanceId);
                    events.Add(GameEvent.CardRevealed(target.OwnerPlayerIndex, target.InstanceId, state.CurrentPhase));
                    events.Add(GameEvent.CardAddedToHand(target.OwnerPlayerIndex, target.InstanceId, state.CurrentPhase));
                    state.Random.Shuffle(context.ActivatingPlayer.MutableDeck);
                    events.Add(GameEvent.DeckShuffled(context.ActivatingPlayerIndex, state.CurrentPhase));
                }
                return true;
            case EffectOperationType.RecoverSelectedCard:
                if (target != null && target.OwnerPlayerIndex == context.ActivatingPlayerIndex && target.Zone == CardZone.Graveyard)
                {
                    context.ActivatingPlayer.MoveGraveyardCardToHand(target.InstanceId);
                    events.Add(GameEvent.CardAddedToHand(target.OwnerPlayerIndex, target.InstanceId, state.CurrentPhase));
                }
                return true;
            case EffectOperationType.RemoveSelectedSupport:
                if (target != null && !target.IsFaceDown && (target.Zone == CardZone.FieldZone || target.Zone == CardZone.SpellTrapZone) &&
                    target.Definition.Category == CardCategory.Spell && target.Definition.SpellType != SpellType.Normal)
                    GameRules.SendSpellTrapToGraveyard(state, target, events);
                return true;
            case EffectOperationType.ChangeSelectedMonsterToAttack:
                if (target != null && target.Zone == CardZone.MonsterZone && target.BattlePosition == BattlePosition.Defense)
                {
                    target.SetBattlePosition(BattlePosition.Attack);
                    events.Add(GameEvent.BattlePositionChanged(target.OwnerPlayerIndex, target.InstanceId, BattlePosition.Attack));
                }
                return true;
            case EffectOperationType.AllocateSelectedEnergy:
                if (target != null && target.Zone == CardZone.MonsterZone && !target.IsDestroyed)
                {
                    target.AllocateEnergy(operation.Amount);
                    events.Add(GameEvent.EnergyAllocated(target.OwnerPlayerIndex, target.InstanceId, operation.Amount));
                }
                return true;
            case EffectOperationType.RemoveSelectedEnergy:
                if (target != null && target.Zone == CardZone.MonsterZone)
                    events.Add(GameEvent.AllocatedEnergyRemoved(target.OwnerPlayerIndex, target.InstanceId,
                        target.RemoveAllocatedEnergy(operation.Amount), state.CurrentPhase));
                return true;
            case EffectOperationType.SwitchToDefenseAfterBattle:
                CardInstance source = state.FindCard(context.SourceInstanceId);
                if (source != null && source.Zone == CardZone.MonsterZone && !source.IsDestroyed &&
                    source.AllocatedEnergy > 0 && source.BattlePosition == BattlePosition.Attack)
                {
                    events.Add(GameEvent.AllocatedEnergyRemoved(source.OwnerPlayerIndex, source.InstanceId,
                        source.RemoveAllocatedEnergy(source.AllocatedEnergy), state.CurrentPhase));
                    source.SetBattlePosition(BattlePosition.Defense);
                    events.Add(GameEvent.BattlePositionChanged(source.OwnerPlayerIndex, source.InstanceId, BattlePosition.Defense));
                }
                return true;
            default: return false;
        }
    }

    private static void RestoreEnergy(EffectContext context, int amount, List<GameEvent> events)
    {
        int before = context.ActivatingPlayer.AvailableEnergy;
        context.ActivatingPlayer.RestoreEnergy(amount);
        int restored = context.ActivatingPlayer.AvailableEnergy - before;
        if (restored > 0) events.Add(GameEvent.EnergyRestored(context.ActivatingPlayerIndex, restored, context.State.CurrentPhase));
    }
}
