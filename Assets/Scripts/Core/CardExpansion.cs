using System;
using System.Collections.Generic;
using System.Linq;

// Shared, name-independent rules for the strategic card expansion.
public static class CardExpansion
{
    public static IReadOnlyList<CardInstance> OwnChoiceCards(GameState state, int player)
    {
        var cards = new Dictionary<long, CardInstance>();
        if (state.InteractionPlayerIndex != player) return new List<CardInstance>();
        foreach (GameAction action in GameRules.GetLegalActions(state, player))
            foreach (long id in action.TargetInstanceIds)
            {
                CardInstance card = state.FindCard(id);
                if (card != null && card.OwnerPlayerIndex == player &&
                    (card.Zone == CardZone.Deck || card.Zone == CardZone.Graveyard)) cards[id] = card;
            }
        return cards.Values.OrderBy(card => card.InstanceId).ToList();
    }
    public static void ValidateCopyLimits(IReadOnlyList<CardDefinition> deck)
    {
        foreach (var group in deck.GroupBy(card => card.CardId))
        {
            int limit = group.Where(card => card.CopyLimitOverride > 0).Select(card => card.CopyLimitOverride).DefaultIfEmpty(0).Min();
            if (limit > 0 && group.Count() > limit)
                throw new ArgumentException("Too many copies of " + group.First().CardName + "; limit " + limit + ".");
        }
    }

    public static EffectOperationDefinition Passive(CardInstance card, EffectOperationType type)
    {
        if (card == null || card.Zone != CardZone.MonsterZone || card.IsFaceDown || card.IsDestroyed) return null;
        foreach (EffectDefinition effect in card.Definition.Effects)
            if (effect.Kind == EffectKind.Continuous && effect.Trigger == EffectTriggerType.WhileOnField)
                foreach (EffectOperationDefinition operation in effect.Operations)
                    if (operation.Type == type) return operation;
        return null;
    }

    public static int PassiveAmount(CardInstance card, EffectOperationType type)
        => Passive(card, type)?.Amount ?? 0;

    public static List<CardInstance> Targets(EffectDefinition effect, EffectContext context)
    {
        PlayerState own = context.ActivatingPlayer, other = context.Opponent;
        IEnumerable<CardInstance> pool;
        switch (effect.TargetType)
        {
            case EffectTargetType.OwnMonster:
            case EffectTargetType.OwnMonsterWithEnergy:
            case EffectTargetType.OwnOtherMonster:
            case EffectTargetType.OwnWoundedMonster: pool = own.MonsterZones; break;
            case EffectTargetType.OpponentMonster:
            case EffectTargetType.OpponentDefenseMonster:
            case EffectTargetType.OpponentMonsterWithEnergy: pool = other.MonsterZones; break;
            case EffectTargetType.OpponentFaceUpSupport:
                pool = other.SpellTrapZones.Concat(new[] { other.FieldSpell }); break;
            case EffectTargetType.OwnDeckHighRankMonster:
            case EffectTargetType.OwnDeckSpellOrTrap: pool = own.Deck; break;
            case EffectTargetType.OwnGraveyardBasicMonster:
            case EffectTargetType.OwnGraveyardNormalTrap: pool = own.Graveyard; break;
            case EffectTargetType.OwnOtherHandCard: pool = own.Hand; break;
            default: return new List<CardInstance>();
        }
        var result = new List<CardInstance>();
        foreach (CardInstance card in pool)
        {
            if (card == null) continue;
            bool eligible = true;
            switch (effect.TargetType)
            {
                case EffectTargetType.OwnMonsterWithEnergy:
                case EffectTargetType.OpponentMonsterWithEnergy: eligible = card.AllocatedEnergy > 0; break;
                case EffectTargetType.OwnOtherMonster:
                case EffectTargetType.OwnOtherHandCard: eligible = card.InstanceId != context.SourceInstanceId; break;
                case EffectTargetType.OwnWoundedMonster: eligible = !card.IsDestroyed && card.CurrentHealth < card.MaximumHealth; break;
                case EffectTargetType.OpponentDefenseMonster: eligible = card.BattlePosition == BattlePosition.Defense; break;
                case EffectTargetType.OpponentFaceUpSupport:
                    eligible = !card.IsFaceDown && card.Definition.Category == CardCategory.Spell &&
                        card.Definition.SpellType != SpellType.Normal; break;
                case EffectTargetType.OwnDeckHighRankMonster:
                    eligible = card.Definition.IsMonster && card.Definition.MonsterRank > MonsterRank.Basic; break;
                case EffectTargetType.OwnDeckSpellOrTrap: eligible = !card.Definition.IsMonster; break;
                case EffectTargetType.OwnGraveyardBasicMonster:
                    eligible = card.Definition.IsMonster && card.Definition.MonsterRank == MonsterRank.Basic; break;
                case EffectTargetType.OwnGraveyardNormalTrap:
                    eligible = card.Definition.Category == CardCategory.Trap && card.Definition.TrapType == TrapType.Normal; break;
            }
            if (eligible) result.Add(card);
        }
        if (effect.TargetType == EffectTargetType.OwnDeckHighRankMonster || effect.TargetType == EffectTargetType.OwnDeckSpellOrTrap ||
            effect.TargetType == EffectTargetType.OwnGraveyardBasicMonster || effect.TargetType == EffectTargetType.OwnGraveyardNormalTrap)
            return result.GroupBy(card => card.Definition.CardId).Select(group => group.OrderBy(card => card.InstanceId).First())
                .OrderBy(card => card.Definition.CardId, StringComparer.Ordinal).ToList();
        return result;
    }
}

public sealed class PositionAuraModifier : IContinuousModifier
{
    private readonly bool defense;
    private readonly int amount;
    public bool IsDefense => defense;
    public int Amount => amount;
    public PositionAuraModifier(bool defense, int amount) { this.defense = defense; this.amount = amount; }
    public int ModifyHealth(CardInstance source, CardInstance target) => 0;
    public int ModifyAttack(CardInstance source, CardInstance target)
        => !defense && Applies(source, target, BattlePosition.Attack) ? amount : 0;
    public int ModifyDefense(CardInstance source, CardInstance target)
        => defense && Applies(source, target, BattlePosition.Defense) ? amount : 0;
    private static bool Applies(CardInstance source, CardInstance target, BattlePosition position)
        => source.OwnerPlayerIndex == target.OwnerPlayerIndex && target.BattlePosition == position;
}
