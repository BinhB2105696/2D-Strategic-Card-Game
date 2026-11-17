using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class GameState
{
    private readonly int[] mainStartMonsterCounts = new int[2];
    private readonly int[] mainStartHealth = new int[2];
    private readonly List<RetaliationMark> retaliationMarks = new List<RetaliationMark>();

    [Serializable]
    private sealed class RetaliationMark
    {
        public long Source;
        public long Opponent;
        public int Reduction;
        public int ExpiresAfterTurn;
    }

    internal void CaptureMainStart()
    {
        for (int player = 0; player < 2; player++)
        {
            mainStartMonsterCounts[player] = players[player].MonsterZones.Count(card => card != null);
            mainStartHealth[player] = players[player].Health;
        }
    }

    public bool HadFewerMonstersAtMainStart(int player)
        => mainStartMonsterCounts[player] < mainStartMonsterCounts[1 - player];
    public int LpDeficitAtMainStart(int player) => mainStartHealth[1 - player] - mainStartHealth[player];

    public int MonsterCountAtMainStart(int player) => mainStartMonsterCounts[player];
    public int HealthAtMainStart(int player) => mainStartHealth[player];

    // Read-only public battle history; unlike ConsumeRetaliationReduction, this never removes a mark.
    public bool TryGetRetaliationMark(long source, long opponent, out int reduction, out int expiresAfterTurn)
    {
        RetaliationMark mark = retaliationMarks.Find(item => item.Source == source && item.Opponent == opponent &&
            item.ExpiresAfterTurn >= TurnNumber && FindCard(item.Source)?.Zone == CardZone.MonsterZone &&
            FindCard(item.Opponent)?.Zone == CardZone.MonsterZone);
        reduction = mark?.Reduction ?? 0;
        expiresAfterTurn = mark?.ExpiresAfterTurn ?? 0;
        return mark != null;
    }

    internal void RefreshMonsterAuras(List<GameEvent> events)
    {
        var expected = new List<StatModifierApplication>();
        foreach (PlayerState owner in players)
            foreach (CardInstance source in owner.MonsterZones)
            {
                if (source == null || source.IsFaceDown || source.IsDestroyed) continue;
                foreach (CardInstance target in owner.MonsterZones)
                {
                    if (target == null || target.IsDestroyed) continue;
                    int attack = 0, defense = 0;
                    foreach (EffectDefinition effect in source.Definition.Effects)
                        if (effect.Kind == EffectKind.Continuous && effect.Trigger == EffectTriggerType.WhileOnField)
                            foreach (IContinuousModifier modifier in effect.ContinuousModifiers)
                            {
                                attack += modifier.ModifyAttack(source, target);
                                defense += modifier.ModifyDefense(source, target);
                            }
                    if (attack != 0 || defense != 0)
                        expected.Add(new StatModifierApplication(source.InstanceId, target, attack, defense, 0, isAura: true));
                }
            }
        RemoveModifiers(old => old.IsAura && !expected.Any(next => next.SourceInstanceId == old.SourceInstanceId &&
            next.Target.InstanceId == old.Target.InstanceId && next.AttackAmount == old.AttackAmount &&
            next.DefenseAmount == old.DefenseAmount), events);
        foreach (StatModifierApplication next in expected)
        {
            if (statModifiers.Any(old => old.IsAura && old.SourceInstanceId == next.SourceInstanceId &&
                old.Target.InstanceId == next.Target.InstanceId)) continue;
            int attackBefore = next.Target.CurrentAttack, defenseBefore = next.Target.CurrentDefense;
            next.Apply(); statModifiers.Add(next);
            if (next.Target.CurrentAttack != attackBefore)
                events.Add(GameEvent.MonsterAttackModified(next.Target.OwnerPlayerIndex, next.Target.InstanceId,
                    next.Target.CurrentAttack - attackBefore, CurrentPhase));
            if (next.Target.CurrentDefense != defenseBefore)
                events.Add(GameEvent.MonsterDefenseModified(next.Target.OwnerPlayerIndex, next.Target.InstanceId,
                    next.Target.CurrentDefense - defenseBefore, CurrentPhase));
        }
    }

    internal void AddRetaliationMark(CardInstance source, CardInstance opponent, int reduction)
    {
        retaliationMarks.RemoveAll(mark => mark.Source == source.InstanceId && mark.Opponent == opponent.InstanceId);
        retaliationMarks.Add(new RetaliationMark { Source = source.InstanceId, Opponent = opponent.InstanceId,
            Reduction = reduction, ExpiresAfterTurn = TurnNumber + 1 });
    }

    internal int ConsumeRetaliationReduction(long attacker, long defender, int damage)
    {
        CleanRetaliationMarks();
        if (damage <= 0) return 0;
        RetaliationMark mark = retaliationMarks.Find(item => item.Source == defender && item.Opponent == attacker);
        if (mark == null) return 0;
        retaliationMarks.Remove(mark);
        return Math.Min(damage, mark.Reduction);
    }

    internal void CleanRetaliationMarks(int completedTurn = -1)
    {
        retaliationMarks.RemoveAll(mark => mark.ExpiresAfterTurn <= completedTurn ||
            mark.ExpiresAfterTurn < TurnNumber || FindCard(mark.Source)?.Zone != CardZone.MonsterZone ||
            FindCard(mark.Opponent)?.Zone != CardZone.MonsterZone);
    }
}
