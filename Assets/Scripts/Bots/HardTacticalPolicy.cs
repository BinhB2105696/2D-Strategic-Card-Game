using System;
using System.Collections.Generic;
using System.Linq;

// H1 preferences over supplied legal actions, never changes legality or game state.
internal sealed class HardTacticalPolicy
{
    private readonly Dictionary<string, CardDefinition> definitions;
    private readonly Dictionary<long, CardInstance> cards = new Dictionary<long, CardInstance>();
    private readonly Dictionary<long, bool> changeable = new Dictionary<long, bool>();
    private readonly Dictionary<long, int> attacks = new Dictionary<long, int>();
    private readonly Dictionary<Tuple<long, long>, int> marks = new Dictionary<Tuple<long, long>, int>();
    private readonly HashSet<long> invested = new HashSet<long>();
    private readonly HashSet<long> attackCommitments = new HashSet<long>();
    private readonly Dictionary<string, bool> defendCache = new Dictionary<string, bool>();
    private readonly Dictionary<GameAction, double> scoreCache = new Dictionary<GameAction, double>();
    private BotGameStateView view;
    private int turn = -1, player, summonCost, zoneCount;
    private double? portfolioBefore;
    private bool canBattle;
    private HashSet<long> reserved = new HashSet<long>();

    internal HardTacticalPolicy(IEnumerable<CardDefinition> catalog) { definitions = catalog.ToDictionary(c => c.CardId); }
    internal void Reset() { invested.Clear(); attackCommitments.Clear(); turn = -1; }
    internal void BeginSimulationFrom(HardTacticalPolicy live)
    { invested.Clear(); invested.UnionWith(live.invested); attackCommitments.Clear(); attackCommitments.UnionWith(live.attackCommitments); turn = live.turn; }
    internal void Observe(GameState state, int actor)
    {
        player = actor; view = BotGameStateView.Create(state, actor);
        if (turn != state.TurnNumber) { invested.Clear(); attackCommitments.Clear(); turn = state.TurnNumber; }
        summonCost = state.Config.NormalSummonEnergyCost;
        zoneCount = state.Config.MonsterZoneCount;
        portfolioBefore = null;
        canBattle = turn > 1 && !state.Players[actor].IsBattlePhaseSkipScheduled(turn);
        cards.Clear(); changeable.Clear(); attacks.Clear(); marks.Clear();
        defendCache.Clear(); scoreCache.Clear();
        foreach (BotPlayerView owner in view.Players)
        {
            foreach (BotCardView c in owner.Hand.Concat(owner.MonsterZones).Concat(owner.SpellTrapZones).Concat(new[] { owner.FieldSpell }))
            {
                if (c == null || !c.IsKnown) continue;
                var copy = new CardInstance(c.InstanceId, c.OwnerPlayerIndex, definitions[c.CardId]);
                copy.MoveToZone(c.Zone, c.IsFaceDown);
                if (copy.Definition.IsMonster)
                {
                    copy.ModifyMaximumHealth(c.MaximumHealth - copy.MaximumHealth);
                    copy.ReceiveDamage(Math.Max(0, copy.MaximumHealth - c.CurrentHealth));
                    if (c.Zone == CardZone.MonsterZone) copy.SetBattlePosition(c.BattlePosition);
                }
                copy.ModifyAttack(c.CurrentAttack - copy.CurrentAttack); copy.ModifyDefense(c.CurrentDefense - copy.CurrentDefense);
                copy.AllocateEnergy(c.AllocatedEnergy); cards[c.InstanceId] = copy;
            }
            foreach (CardInstance c in state.Players[owner.PlayerIndex].MonsterZones)
                if (c != null && (owner.PlayerIndex == actor || !c.IsFaceDown))
                {
                    changeable[c.InstanceId] = !c.WasSummonedThisTurn && !c.HasChangedPositionThisTurn;
                    attacks[c.InstanceId] = Math.Max(0, c.MaxAttacksThisTurn - c.AttacksUsedThisTurn);
                    if (c.WasSummonedThisTurn && CardExpansion.PassiveAmount(c, EffectOperationType.CannotAttackOnSummonTurn) > 0)
                        attacks[c.InstanceId] = 0;
                }
        }
        foreach (CardInstance a in cards.Values.Where(c => c.Zone == CardZone.MonsterZone))
            foreach (CardInstance b in cards.Values.Where(c => c.Zone == CardZone.MonsterZone && c.OwnerPlayerIndex != a.OwnerPlayerIndex))
                if (state.TryGetRetaliationMark(b.InstanceId, a.InstanceId, out int reduction, out int expires))
                    marks[Tuple.Create(a.InstanceId, b.InstanceId)] = reduction;
        attackCommitments.RemoveWhere(id => Find(id) == null || Find(id).Zone != CardZone.MonsterZone || Uses(Find(id)) == 0
            || Find(id).BattlePosition == BattlePosition.Defense && !CanTurn(Find(id)));
    }
    private CardInstance Find(long id) => cards.TryGetValue(id, out CardInstance c) ? c : null;
    private IEnumerable<CardInstance> Board(int owner) => cards.Values.Where(c => c.OwnerPlayerIndex == owner && c.Zone == CardZone.MonsterZone);
    private bool CanTurn(CardInstance c) => changeable.TryGetValue(c.InstanceId, out bool yes) && yes;
    private int Uses(CardInstance c) => c.Zone == CardZone.Hand
        ? ((HardStrategicEvaluation.ProspectivePassive(c, EffectOperationType.CannotAttackOnSummonTurn)?.Amount ?? 0) > 0 ? 0 : 1)
        : attacks.TryGetValue(c.InstanceId, out int n) ? n : 0;
    private int AttackAt(CardInstance c, BattlePosition position)
    {
        int value = c.CurrentAttack + SummonFieldBonus(c, position, true);
        foreach (CardInstance source in Board(c.OwnerPlayerIndex))
            foreach (EffectDefinition effect in source.Definition.Effects)
                foreach (IContinuousModifier modifier in effect.ContinuousModifiers)
                    if (modifier is PositionAuraModifier aura && !aura.IsDefense)
                        value += (position == BattlePosition.Attack ? aura.Amount : 0)
                            - (c.Zone == CardZone.MonsterZone && c.BattlePosition == BattlePosition.Attack ? aura.Amount : 0);
        return Math.Max(0, value);
    }
    private int DefenseAt(CardInstance c, BattlePosition position)
    {
        int value = c.CurrentDefense + SummonFieldBonus(c, position, false);
        foreach (CardInstance source in Board(c.OwnerPlayerIndex))
            foreach (EffectDefinition effect in source.Definition.Effects)
                foreach (IContinuousModifier modifier in effect.ContinuousModifiers)
                    if (modifier is PositionAuraModifier aura && aura.IsDefense)
                        value += (position == BattlePosition.Defense ? aura.Amount : 0)
                            - (c.Zone == CardZone.MonsterZone && c.BattlePosition == BattlePosition.Defense ? aura.Amount : 0);
        return Math.Max(0, value);
    }
    private int SummonFieldBonus(CardInstance c, BattlePosition position, bool attack)
    {
        BotCardView field = view.Players[c.OwnerPlayerIndex].FieldSpell;
        if (c.Zone != CardZone.Hand || field == null) return 0;
        CardInstance source = Find(field.InstanceId);
        if (source == null) return 0;
        CardInstance target = c.CopyForSearch();
        target.MoveToZone(CardZone.MonsterZone, false); target.SetBattlePosition(position);
        return source.Definition.Effects.Sum(e => e.ContinuousModifiers.Sum(m => attack
            ? m.ModifyAttack(source, target) : m.ModifyDefense(source, target)));
    }
    private int Damage(CardInstance attacker, CardInstance target, BattlePosition position)
    {
        int damage = AttackAt(attacker, BattlePosition.Attack);
        if (position == BattlePosition.Defense)
            damage = Math.Max(0, damage - Math.Max(0, DefenseAt(target, position)
                - (HardStrategicEvaluation.ProspectivePassive(attacker, EffectOperationType.PierceDefense)?.Amount ?? 0)));
        if (marks.TryGetValue(Tuple.Create(attacker.InstanceId, target.InstanceId), out int reduction)) damage = Math.Max(0, damage - reduction);
        return damage;
    }
    private bool OneHit(CardInstance c, BattlePosition position) => Board(1 - player).Any(enemy =>
        enemy.Definition.AttackEnergyRequirement <= view.Players[1 - player].MaxEnergy
        && Damage(enemy, c, position) >= c.CurrentHealth);
    private bool Aimable(CardInstance target)
    {
        int priority = Board(1 - player).Select(c => c.AttackPriority).DefaultIfEmpty(0).Max();
        return target.AttackPriority == priority;
    }
    private bool FavorableHit(CardInstance c, CardInstance target)
    {
        int amount = Math.Min(target.CurrentHealth, Damage(c, target, target.BattlePosition));
        if (amount <= 0) return false;
        int reflected = target.BattlePosition == BattlePosition.Defense
            ? Math.Min(amount, CardExpansion.PassiveAmount(target, EffectOperationType.ReflectBattleDamage)) : 0;
        // Reflection precedes lifesteal in the real rules; a KO cannot be healed.
        if (reflected >= c.CurrentHealth) return false;
        EffectOperationDefinition lifesteal = HardStrategicEvaluation.ProspectivePassive(c, EffectOperationType.Lifesteal);
        int restored = lifesteal == null ? 0 : Math.Min(c.MaximumHealth - c.CurrentHealth + reflected,
            Math.Min(lifesteal.SecondaryAmount, amount * lifesteal.Amount / 100));
        return amount > reflected - restored || amount >= target.CurrentHealth;
    }
    private int WaveDamage(CardInstance target, IEnumerable<CardInstance> squad, int energy, bool nextWave)
    {
        var list = squad.Take(6).ToArray(); int best = 0;
        for (int mask = 1; mask < (1 << list.Length); mask++)
        {
            int cost = 0, total = 0, used = 0;
            for (int i = 0; i < list.Length; i++) if ((mask & (1 << i)) != 0)
            {
                CardInstance c = list[i];
                used++;
                if (Uses(c) == 0 || (nextWave && used > 3) || (!nextWave && c.Zone == CardZone.MonsterZone && c.BattlePosition == BattlePosition.Defense && !CanTurn(c))) { cost = int.MaxValue / 2; break; }
                cost += Math.Max(0, c.Definition.AttackEnergyRequirement - c.AllocatedEnergy) + (nextWave ? summonCost : 0);
                if (FavorableHit(c, target)) total += Damage(c, target, target.BattlePosition) * (nextWave ? 1 : Math.Max(1, Uses(c)));
            }
            if (cost <= energy) best = Math.Max(best, total);
        }
        return best;
    }
    internal bool ShouldDefend(CardInstance c, int energy, IReadOnlyList<long> tributes = null)
    {
        string key = c.InstanceId + ":" + energy + ":" + (tributes == null ? "" : string.Join(",", tributes));
        if (!defendCache.TryGetValue(key, out bool answer))
        { answer = EvaluateDefense(c, energy, tributes); defendCache[key] = answer; }
        return answer;
    }
    private bool EvaluateDefense(CardInstance c, int energy, IReadOnlyList<long> tributes)
    {
        if (!canBattle || Uses(c) == 0 || Math.Max(0, c.Definition.AttackEnergyRequirement - c.AllocatedEnergy) > energy) return true;
        var enemies = Board(1 - player).Where(Aimable).ToArray();
        if (enemies.Length == 0) return AttackAt(c, BattlePosition.Attack) <= 0;
        var good = enemies.Where(target => FavorableHit(c, target)).ToArray();
        if (good.Length == 0) return true;
        if (!OneHit(c, BattlePosition.Attack)) return false;
        var team = Board(player).Where(card => tributes == null || !tributes.Contains(card.InstanceId)).ToList();
        if (!team.Any(card => card.InstanceId == c.InstanceId)) team.Add(c);
        var basics = view.Players[player].Hand.Where(card => card.Category == CardCategory.Monster && card.MonsterRank == MonsterRank.Basic)
            .Select(card => Find(card.InstanceId)).Where(card => card != null && card.InstanceId != c.InstanceId).ToArray();
        foreach (CardInstance target in good)
        {
            int wave = WaveDamage(target, team, energy, false);
            if (wave >= target.CurrentHealth) return false;
            if (target.Definition.MonsterRank != MonsterRank.Basic && wave * 2 > target.CurrentHealth && basics.Length > 0
                && WaveDamage(target, basics, view.Players[player].MaxEnergy, true) >= target.CurrentHealth - wave) return false;
        }
        return true;
    }
    private bool EnergyFuel(CardInstance c) => c.Definition.Effects.Any(e =>
        e.Costs.Any(cost => cost.Type == EffectCostType.RemoveAllocatedEnergy)
        || e.Operations.Any(op => op.Type == EffectOperationType.RestoreHealthPerRemovedAllocatedEnergy));
    private double TributeLoss(CardInstance c)
    {
        double value = c.CurrentHealth * 0.25 + c.CurrentAttack * 0.5 + c.CurrentDefense * 0.2 + c.AllocatedEnergy * 80;
        if (c.Definition.Effects.Any(e => e.ContinuousModifiers.Count > 0)) value += 900;
        if (c.Definition.Effects.Any(e => e.Trigger == EffectTriggerType.OnDestroyed)) value += 450;
        if (c.Definition.Effects.Any(e => e.Trigger == EffectTriggerType.AfterBattle || e.Operations.Any(op => op.Type == EffectOperationType.ReflectBattleDamage))) value += 250;
        if (OneHit(c, c.BattlePosition)) value *= 0.25;
        if (invested.Contains(c.InstanceId)) value += 1800;
        return value;
    }
    internal double Score(BotGameStateView state, GameAction a, NormalBot guide)
    {
        if (scoreCache.TryGetValue(a, out double cached)) return cached;
        double score = guide.ScoreForSearch(state, a); CardInstance c = Find(a.SourceInstanceId);
        if (a.Type == GameActionType.SummonMonster)
        {
            score += SummonPortfolioChange(c, a) * 0.45;
            foreach (long id in a.TributeInstanceIds) if (Find(id) != null) score -= TributeLoss(Find(id));
            if (a.BattlePosition == BattlePosition.Defense) score += Math.Min(c.CurrentDefense, Board(1 - player).Select(e => e.CurrentAttack).DefaultIfEmpty(0).Max()) * 0.2;
            if (a.BattlePosition == BattlePosition.Attack && ShouldDefend(c, state.Players[player].AvailableEnergy - summonCost, a.TributeInstanceIds)) score -= 180;
        }
        if ((a.Type == GameActionType.ActivateCard || a.Type == GameActionType.ActivateNormalSpell) && c != null)
            score += HardStrategicEvaluation.DrawActionBonus(c, state.Players[player].HandCount,
                state.Players[player].DeckCount, !Board(player).Any());
        if (a.Type == GameActionType.ChangeBattlePosition && a.BattlePosition == BattlePosition.Defense && ShouldDefend(c, state.Players[player].AvailableEnergy)) score += 1600;
        if (a.Type == GameActionType.ChangeBattlePosition && a.BattlePosition == BattlePosition.Attack && ShouldDefend(c, state.Players[player].AvailableEnergy)) score -= 180;
        scoreCache[a] = score;
        return score;
    }
    private double Portfolio(IEnumerable<CardInstance> hand, IEnumerable<CardInstance> board, int energy)
        => HardStrategicEvaluation.PortfolioBonus(hand, board, Board(1 - player), view.Players[player].DeckCount,
            energy, view.Players[player].MaxEnergy, view.Players[1 - player].MaxEnergy, summonCost, zoneCount);
    private double SummonPortfolioChange(CardInstance card, GameAction action)
    {
        CardInstance[] hand = cards.Values.Where(c => c.OwnerPlayerIndex == player && c.Zone == CardZone.Hand).ToArray();
        int energy = view.Players[player].AvailableEnergy;
        if (!portfolioBefore.HasValue) portfolioBefore = Portfolio(hand, Board(player), energy);
        CardInstance projected = card.CopyForSearch();
        projected.MoveToZone(CardZone.MonsterZone, false); projected.SetBattlePosition(action.BattlePosition);
        return Portfolio(hand.Where(c => c.InstanceId != card.InstanceId),
            Board(player).Where(c => !action.TributeInstanceIds.Contains(c.InstanceId)).Concat(new[] { projected }),
            energy - summonCost) - portfolioBefore.Value;
    }
    private int EnergyNeed(CardInstance c) => Math.Max(0, c.Definition.AttackEnergyRequirement - c.AllocatedEnergy);
    private int CommittedEnergy() => attackCommitments.Where(id => Find(id) != null && Find(id).Zone == CardZone.MonsterZone).Sum(id => EnergyNeed(Find(id)));
    private bool AttackOpportunity(CardInstance c) => canBattle && Uses(c) > 0 &&
        (!Board(1 - player).Any() ? AttackAt(c, BattlePosition.Attack) > 0 : Board(1 - player).Where(Aimable).Any(target => FavorableHit(c, target)));
    private bool MustDefend(CardInstance c, int energy) => !AttackOpportunity(c) || EnergyNeed(c) > energy;
    private GameAction Preparation(BotGameStateView state, IReadOnlyList<GameAction> legal)
    {
        if (state.CurrentPhase != TurnPhase.Main || !canBattle) return null;
        int energy = state.Players[player].AvailableEnergy;
        foreach (CardInstance c in Board(player).OrderByDescending(c => attackCommitments.Contains(c.InstanceId)).ThenBy(c => EnergyNeed(c)).ThenBy(c => c.InstanceId))
        {
            if (reserved.Contains(c.InstanceId) || !AttackOpportunity(c) || EnergyNeed(c) > energy) continue;
            if (c.BattlePosition == BattlePosition.Defense && !CanTurn(c)) continue;
            if (!attackCommitments.Contains(c.InstanceId) && ShouldDefend(c, energy)) continue;
            int needed = EnergyNeed(c);
            GameAction step = needed > 0
                ? legal.FirstOrDefault(a => a.Type == GameActionType.AllocateEnergy && a.SourceInstanceId == c.InstanceId && a.Amount == needed)
                : c.BattlePosition == BattlePosition.Defense
                    ? legal.FirstOrDefault(a => a.Type == GameActionType.ChangeBattlePosition && a.SourceInstanceId == c.InstanceId && a.BattlePosition == BattlePosition.Attack) : null;
            if (step != null) return step;
        }
        return null;
    }
    private bool ReadyAttack() => canBattle && Board(player).Any(c => c.BattlePosition == BattlePosition.Attack && EnergyNeed(c) == 0 && AttackOpportunity(c));
    private int KnownEnergyCost(GameAction action)
    {
        if (action.Type == GameActionType.SummonMonster) return summonCost;
        if (action.Type == GameActionType.AllocateEnergy) return action.Amount;
        CardInstance c = Find(action.SourceInstanceId);
        if (action.Type != GameActionType.ActivateCard || c == null) return 0;
        // Conservative lower bound on explicit printed costs; never guesses hidden effects or changes actual costs.
        return c.Definition.Effects.Where(e => e.Trigger == EffectTriggerType.ManualActivation)
            .Select(e => e.Costs.Sum(cost => cost.Type == EffectCostType.AvailableEnergy ? cost.Amount
                : cost.Type == EffectCostType.EnergyWithLpDeficitDiscount && view.MainStartLpDeficit < cost.SecondaryAmount ? cost.Amount : 0)).DefaultIfEmpty(0).Min();
    }
    private bool UnusableTemporaryBuff(GameAction a, int energy)
    {
        CardInstance source = Find(a.SourceInstanceId);
        if (a.Type != GameActionType.ActivateCard || source == null) return false;
        var effects = source.Definition.Effects.Where(e => e.Trigger == EffectTriggerType.ManualActivation && e.Operations.Count > 0).ToArray();
        if (effects.Length == 0 || effects.Any(e => e.Operations.Any(op => op.Type != EffectOperationType.IncreaseAttackUntilEndOfTurn))) return false;
        CardInstance target = Find(a.TargetInstanceId == 0 ? a.SourceInstanceId : a.TargetInstanceId);
        return target == null || !canBattle || Uses(target) == 0 || reserved.Contains(target.InstanceId)
            || (target.BattlePosition == BattlePosition.Defense && !CanTurn(target)) || EnergyNeed(target) > energy - KnownEnergyCost(a);
    }
    internal IReadOnlyList<GameAction> Filter(BotGameStateView state, IReadOnlyList<GameAction> legal, NormalBot guide)
    {
        if (state.HasOpenResponseWindow || state.HasPendingTriggeredEffect) return legal;
        int energy = state.Players[player].AvailableEnergy;
        reserved.Clear();
        GameAction planned = legal.Where(a => a.Type == GameActionType.SummonMonster)
            .OrderByDescending(a => Score(state, a, guide)).FirstOrDefault();
        if (planned != null && Score(state, planned, guide) > 0
            && Score(state, planned, guide) >= legal.Where(a => a.Type != GameActionType.SummonMonster).Select(a => Score(state, a, guide)).DefaultIfEmpty(0).Max())
            reserved.UnionWith(planned.TributeInstanceIds);
        bool fixPosture = legal.Any(a => a.Type == GameActionType.ChangeBattlePosition && a.BattlePosition == BattlePosition.Defense && MustDefend(Find(a.SourceInstanceId), energy));
        bool goodAttack = legal.Any(a => a.Type == GameActionType.Attack && GoodAttack(a, energy));
        GameAction preparation = Preparation(state, legal);
        int debt = CommittedEnergy();
        var result = new List<GameAction>();
        foreach (GameAction a in legal)
        {
            CardInstance c = Find(a.SourceInstanceId);
            int remainingDebt = debt - a.TributeInstanceIds.Where(id => attackCommitments.Contains(id) && Find(id) != null).Sum(id => EnergyNeed(Find(id)));
            if (a.Type == GameActionType.SummonMonster && a.TributeInstanceIds.Any(id => invested.Contains(id))) continue;
            if (a.Type == GameActionType.SummonMonster && a.BattlePosition == BattlePosition.Attack &&
                (!canBattle || Uses(c) == 0 || energy - summonCost < remainingDebt + EnergyNeed(c))) continue;
            if (a.Type == GameActionType.ChangeBattlePosition && a.BattlePosition == BattlePosition.Attack &&
                (!canBattle || Uses(c) == 0 || energy < debt + (attackCommitments.Contains(c.InstanceId) ? 0 : EnergyNeed(c)))) continue;
            if (a.Type == GameActionType.AllocateEnergy && (reserved.Contains(c.InstanceId)
                || (!EnergyFuel(c) && (!AttackOpportunity(c) || c.AllocatedEnergy + a.Amount > c.Definition.AttackEnergyRequirement)))) continue;
            if (remainingDebt > 0 && energy >= remainingDebt && !(a.Type == GameActionType.AllocateEnergy && attackCommitments.Contains(a.SourceInstanceId)) && energy - KnownEnergyCost(a) < remainingDebt) continue;
            if (UnusableTemporaryBuff(a, energy)) continue;
            if (state.CurrentPhase == TurnPhase.Main && fixPosture && (a.Type == GameActionType.AdvancePhase || a.Type == GameActionType.EnterEndPhase)) continue;
            if (state.CurrentPhase == TurnPhase.Main && preparation != null && (a.Type == GameActionType.AdvancePhase || a.Type == GameActionType.EnterEndPhase)) continue;
            if (state.CurrentPhase == TurnPhase.Main && ReadyAttack() && a.Type == GameActionType.EnterEndPhase) continue;
            if (a.Type == GameActionType.Attack && !GoodAttack(a, energy)) continue;
            if (a.Type == GameActionType.EnterEndPhase && goodAttack) continue;
            result.Add(a);
        }
        return result.Count > 0 ? result : legal; // Preferences cannot invent a move or deadlock the game.
    }
    private bool GoodAttack(GameAction a, int energy)
    {
        CardInstance c = Find(a.SourceInstanceId), target = Find(a.TargetInstanceId);
        if (c == null) return false;
        if (target == null) return c.CurrentAttack > 0;
        // Stance is already locked in Battle. Do not abandon a coordinated wave halfway
        // through by recomputing its half-HP threshold against the now-wounded target.
        return FavorableHit(c, target);
    }
    internal GameAction Choose(BotGameStateView state, IReadOnlyList<GameAction> legal, NormalBot guide)
    {
        var choices = Filter(state, legal, guide);
        GameAction prepare = Preparation(state, choices);
        if (prepare != null) return prepare;
        double best = choices.Max(a => Score(state, a, guide));
        return guide.ChooseAction(state, choices.Where(a => Math.Abs(Score(state, a, guide) - best) < 0.000001).ToArray());
    }
    internal void Commit(GameAction a)
    {
        if (a.Type == GameActionType.AllocateEnergy) invested.Add(a.SourceInstanceId);
        if (a.Type == GameActionType.SummonMonster && a.BattlePosition == BattlePosition.Attack ||
            a.Type == GameActionType.ChangeBattlePosition && a.BattlePosition == BattlePosition.Attack || a.Type == GameActionType.AllocateEnergy)
            attackCommitments.Add(a.SourceInstanceId);
        if (a.Type == GameActionType.Attack) { invested.Remove(a.SourceInstanceId); attackCommitments.Remove(a.SourceInstanceId); }
        if (a.Type == GameActionType.ChangeBattlePosition && a.BattlePosition == BattlePosition.Defense) attackCommitments.Remove(a.SourceInstanceId);
    }
}
