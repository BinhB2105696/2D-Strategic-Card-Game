using System;
using System.Collections.Generic;
using System.Linq;

// H1 preferences over supplied legal actions, never changes legality or game state.
internal sealed class HardTacticalPolicy
{
    private struct Exposure
    {
        internal int AttackDamage;
        internal int DefenseDamage;
        internal Exposure(int attackDamage, int defenseDamage)
        { AttackDamage = attackDamage; DefenseDamage = defenseDamage; }
    }
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
    private readonly HashSet<long> sequentialOpeners = new HashSet<long>();
    private readonly HashSet<long> sequentialFollowers = new HashSet<long>();
    internal int LastCancelledCommitments { get; private set; }
    internal int LastPlannedAttackers { get; private set; }
    internal double LastAttackPlanScore { get; private set; }
    internal bool LastAttackPlanFallback { get; private set; }

    internal HardTacticalPolicy(IEnumerable<CardDefinition> catalog) { definitions = catalog.ToDictionary(c => c.CardId); }
    internal void Reset() { invested.Clear(); attackCommitments.Clear(); turn = -1; LastCancelledCommitments = LastPlannedAttackers = 0; LastAttackPlanScore = 0; LastAttackPlanFallback = false; }
    internal void BeginSimulationFrom(HardTacticalPolicy live)
    { invested.Clear(); invested.UnionWith(live.invested); attackCommitments.Clear(); attackCommitments.UnionWith(live.attackCommitments); turn = live.turn; LastCancelledCommitments = LastPlannedAttackers = 0; LastAttackPlanScore = 0; LastAttackPlanFallback = false; }
    internal void Observe(GameState state, int actor)
    {
        player = actor; view = BotGameStateView.Create(state, actor);
        LastCancelledCommitments = LastPlannedAttackers = 0; LastAttackPlanScore = 0; LastAttackPlanFallback = false;
        sequentialOpeners.Clear(); sequentialFollowers.Clear();
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
        LastCancelledCommitments += attackCommitments.RemoveWhere(id => Find(id) == null || Find(id).Zone != CardZone.MonsterZone || Uses(Find(id)) == 0
            || Find(id).BattlePosition == BattlePosition.Defense && !CanTurn(Find(id)));
    }
    private CardInstance Find(long id) => cards.TryGetValue(id, out CardInstance c) ? c : null;
    private IEnumerable<CardInstance> Board(int owner) => cards.Values.Where(c => c.OwnerPlayerIndex == owner && c.Zone == CardZone.MonsterZone);
    private bool CanTurn(CardInstance c) => changeable.TryGetValue(c.InstanceId, out bool yes) && yes;
    private int Uses(CardInstance c) => c.Zone == CardZone.Hand
        ? ((HardStrategicEvaluation.ProspectivePassive(c, EffectOperationType.CannotAttackOnSummonTurn)?.Amount ?? 0) > 0 ? 0 : 1)
        : attacks.TryGetValue(c.InstanceId, out int n) ? n : 0;
    private int AttackAt(CardInstance c, BattlePosition position, long excludedAuraSource = 0)
    {
        int value = c.CurrentAttack + SummonFieldBonus(c, position, true);
        foreach (CardInstance source in Board(c.OwnerPlayerIndex))
            foreach (EffectDefinition effect in source.Definition.Effects)
                foreach (IContinuousModifier modifier in effect.ContinuousModifiers)
                    if (modifier is PositionAuraModifier aura && !aura.IsDefense)
                        value += (source.InstanceId != excludedAuraSource && position == BattlePosition.Attack ? aura.Amount : 0)
                            - (c.Zone == CardZone.MonsterZone && c.BattlePosition == BattlePosition.Attack ? aura.Amount : 0);
        return Math.Max(0, value);
    }
    private int DefenseAt(CardInstance c, BattlePosition position, long excludedAuraSource = 0)
    {
        int value = c.CurrentDefense + SummonFieldBonus(c, position, false);
        foreach (CardInstance source in Board(c.OwnerPlayerIndex))
            foreach (EffectDefinition effect in source.Definition.Effects)
                foreach (IContinuousModifier modifier in effect.ContinuousModifiers)
                    if (modifier is PositionAuraModifier aura && aura.IsDefense)
                        value += (source.InstanceId != excludedAuraSource && position == BattlePosition.Defense ? aura.Amount : 0)
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
    private int Damage(CardInstance attacker, CardInstance target, BattlePosition position, long excludedAuraSource = 0)
    {
        int damage = AttackAt(attacker, BattlePosition.Attack, excludedAuraSource);
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
    private Exposure IncomingExposure(CardInstance target, long excludedEnemy = 0)
    {
        CardInstance[] enemies = Board(1 - player).Where(c => c.InstanceId != excludedEnemy).Take(6).ToArray();
        int bestAttack = 0, bestDefense = 0, available = view.Players[1 - player].MaxEnergy;
        for (int mask = 1; mask < (1 << enemies.Length); mask++)
        {
            int cost = 0, used = 0, attack = 0, defense = 0;
            for (int i = 0; i < enemies.Length; i++) if ((mask & (1 << i)) != 0)
            {
                if (++used > 3) { cost = int.MaxValue / 2; break; }
                CardInstance enemy = enemies[i];
                cost += Math.Max(0, enemy.Definition.AttackEnergyRequirement - enemy.AllocatedEnergy);
                attack += Damage(enemy, target, BattlePosition.Attack, excludedEnemy);
                defense += Damage(enemy, target, BattlePosition.Defense, excludedEnemy);
            }
            if (cost <= available)
            {
                bestAttack = Math.Max(bestAttack, attack);
                bestDefense = Math.Max(bestDefense, defense);
            }
        }
        return new Exposure(bestAttack, bestDefense);
    }
    private double ExposurePenalty(CardInstance c, Exposure exposure)
    {
        if (c == null || c.CurrentHealth <= 0) return 0;
        int attackLoss = Math.Min(c.CurrentHealth, exposure.AttackDamage);
        int defenseLoss = Math.Min(c.CurrentHealth, exposure.DefenseDamage);
        int avoidable = Math.Max(0, attackLoss - defenseLoss);
        double pressure = attackLoss / (double)c.CurrentHealth;
        double penalty = avoidable * 0.45 + Math.Max(0, pressure - 0.5) * 300;
        if (exposure.AttackDamage >= c.CurrentHealth) penalty += 350;
        return Math.Min(1000, penalty);
    }
    private bool SeriousExposure(CardInstance c, Exposure exposure)
    {
        if (exposure.AttackDamage >= c.CurrentHealth) return true;
        int avoidable = Math.Max(0, exposure.AttackDamage - exposure.DefenseDamage);
        return (long)exposure.AttackDamage * 4 >= (long)c.CurrentHealth * 3
            && avoidable >= Math.Max(150, c.CurrentHealth / 4);
    }
    private Exposure BestExposureAfterAttack(CardInstance c, int energy, IReadOnlyList<long> tributes)
    {
        Exposure best = IncomingExposure(c);
        double bestPenalty = ExposurePenalty(c, best);
        var team = Board(player).Where(card => tributes == null || !tributes.Contains(card.InstanceId)).ToList();
        if (!team.Any(card => card.InstanceId == c.InstanceId)) team.Add(c);
        foreach (CardInstance target in Board(1 - player).Where(Aimable).Where(target => FavorableHit(c, target)))
        {
            if (WaveDamage(target, team, energy, false) < target.CurrentHealth) continue;
            Exposure after = IncomingExposure(c, target.InstanceId);
            double penalty = ExposurePenalty(c, after);
            if (penalty < bestPenalty) { best = after; bestPenalty = penalty; }
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
        Exposure exposure = BestExposureAfterAttack(c, energy, tributes);
        if (!SeriousExposure(c, exposure)) return false;
        var team = Board(player).Where(card => tributes == null || !tributes.Contains(card.InstanceId)).ToList();
        if (!team.Any(card => card.InstanceId == c.InstanceId)) team.Add(c);
        var basics = view.Players[player].Hand.Where(card => card.Category == CardCategory.Monster && card.MonsterRank == MonsterRank.Basic)
            .Select(card => Find(card.InstanceId)).Where(card => card != null && card.InstanceId != c.InstanceId).ToArray();
        foreach (CardInstance target in good)
        {
            int wave = WaveDamage(target, team, energy, false);
            if (target.Definition.MonsterRank != MonsterRank.Basic && wave * 2 > target.CurrentHealth && basics.Length > 0
                && WaveDamage(target, basics, view.Players[player].MaxEnergy, true) >= target.CurrentHealth - wave) return false;
        }
        return true;
    }
    private bool LegacyShouldDefend(CardInstance c, int energy)
    {
        if (!canBattle || Uses(c) == 0 || EnergyNeed(c) > energy) return true;
        var enemies = Board(1 - player).Where(Aimable).ToArray();
        if (enemies.Length == 0) return AttackAt(c, BattlePosition.Attack) <= 0;
        var good = enemies.Where(target => FavorableHit(c, target)).ToArray();
        if (good.Length == 0) return true;
        if (!OneHit(c, BattlePosition.Attack)) return false;
        var team = Board(player).ToList();
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
            if (a.BattlePosition == BattlePosition.Attack)
            {
                int energy = state.Players[player].AvailableEnergy - summonCost;
                score -= Math.Max(ShouldDefend(c, energy, a.TributeInstanceIds) ? 180 : 0,
                    ExposurePenalty(c, BestExposureAfterAttack(c, energy, a.TributeInstanceIds)));
            }
        }
        if ((a.Type == GameActionType.ActivateCard || a.Type == GameActionType.ActivateNormalSpell) && c != null)
            score += HardStrategicEvaluation.DrawActionBonus(c, state.Players[player].HandCount,
                state.Players[player].DeckCount, !Board(player).Any());
        if (a.Type == GameActionType.ChangeBattlePosition && a.BattlePosition == BattlePosition.Defense && ShouldDefend(c, state.Players[player].AvailableEnergy)) score += 1600;
        if (a.Type == GameActionType.ChangeBattlePosition && a.BattlePosition == BattlePosition.Attack)
        {
            int energy = state.Players[player].AvailableEnergy;
            score -= Math.Max(ShouldDefend(c, energy) ? 180 : 0,
                ExposurePenalty(c, BestExposureAfterAttack(c, energy, null)));
        }
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
    private bool AttackPreparationOpportunity(CardInstance c)
        => AttackOpportunity(c) || sequentialOpeners.Contains(c.InstanceId) || sequentialFollowers.Contains(c.InstanceId);
    private HashSet<long> SequentialFallbackPlan(int energy, out double bestScore)
    {
        bestScore = 0;
        var best = new HashSet<long>();
        var bestOpeners = new HashSet<long>();
        CardInstance[] team = Board(player).Where(c => !reserved.Contains(c.InstanceId) && Uses(c) > 0 && AttackAt(c, BattlePosition.Attack) > 0
            && (c.BattlePosition == BattlePosition.Attack || CanTurn(c))).Take(6).ToArray();
        foreach (CardInstance target in Board(1 - player).Where(Aimable))
            for (int openingMask = 1; openingMask < (1 << team.Length); openingMask++)
            {
                var openers = new List<CardInstance>(); int cost = 0, damage = 0;
                for (int i = 0; i < team.Length; i++) if ((openingMask & (1 << i)) != 0)
                {
                    CardInstance opener = team[i];
                    if (!FavorableHit(opener, target)) { cost = int.MaxValue / 2; break; }
                    openers.Add(opener); cost += EnergyNeed(opener);
                    damage += Damage(opener, target, target.BattlePosition) * Math.Max(1, Uses(opener));
                }
                if (cost > energy || damage < target.CurrentHealth) continue;
                CardInstance[] remainingEnemies = Board(1 - player).Where(c => c.InstanceId != target.InstanceId).ToArray();
                int nextPriority = remainingEnemies.Select(c => c.AttackPriority).DefaultIfEmpty(int.MinValue).Max();
                var nextTargets = remainingEnemies.Where(c => c.AttackPriority == nextPriority).ToArray();
                var followers = new List<CardInstance>();
                var remainingHealth = nextTargets.ToDictionary(c => c.InstanceId, c => c.CurrentHealth);
                double score = target.CurrentHealth * 1.7 + target.CurrentAttack * 0.35
                    + target.AllocatedEnergy * 100 + target.Definition.TributeCount * 220;
                foreach (CardInstance follower in team.Where((c, index) => (openingMask & (1 << index)) == 0)
                    .OrderBy(c => EnergyNeed(c)).ThenBy(c => c.InstanceId))
                {
                    int followerCost = EnergyNeed(follower);
                    if (cost + followerCost > energy) continue;
                    double value;
                    if (nextTargets.Length == 0)
                        value = AttackAt(follower, BattlePosition.Attack, target.InstanceId) * Math.Max(1, Uses(follower));
                    else
                    {
                        CardInstance followTarget = nextTargets.Where(t => remainingHealth[t.InstanceId] > 0 && FavorableHit(follower, t))
                            .OrderByDescending(t => Math.Min(remainingHealth[t.InstanceId], Damage(follower, t, t.BattlePosition, target.InstanceId)))
                            .FirstOrDefault();
                        if (followTarget == null) continue;
                        int dealt = Math.Min(remainingHealth[followTarget.InstanceId], Damage(follower, followTarget, followTarget.BattlePosition, target.InstanceId));
                        remainingHealth[followTarget.InstanceId] -= dealt; value = dealt * Math.Max(1, Uses(follower)) * 0.45;
                    }
                    value -= followerCost * 40 + ExposurePenalty(follower, IncomingExposure(follower, target.InstanceId)) * 0.25;
                    if (value <= 0) continue;
                    followers.Add(follower); cost += followerCost; score += value;
                }
                if (followers.Count == 0) continue;
                score -= openers.Select(c => ExposurePenalty(c, IncomingExposure(c, target.InstanceId))).DefaultIfEmpty(0).Max() * 0.25;
                score -= openers.Sum(EnergyNeed) * 40;
                if (score <= bestScore + 0.000001) continue;
                bestScore = score; best = new HashSet<long>(openers.Concat(followers).Select(c => c.InstanceId));
                bestOpeners = new HashSet<long>(openers.Select(c => c.InstanceId));
            }
        sequentialOpeners.UnionWith(bestOpeners);
        sequentialFollowers.UnionWith(best.Where(id => !bestOpeners.Contains(id)));
        return best;
    }
    private HashSet<long> JointAttackPlan(int energy)
    {
        sequentialOpeners.Clear(); sequentialFollowers.Clear();
        var candidates = Board(player).Where(c => !reserved.Contains(c.InstanceId) && AttackOpportunity(c)
            && (c.BattlePosition == BattlePosition.Attack || CanTurn(c))).Take(6).ToArray();
        var best = new HashSet<long>(); double bestScore = 0;
        CardInstance[] targets = Board(1 - player).Where(Aimable).ToArray();
        for (int mask = 1; mask < (1 << candidates.Length); mask++)
        {
            var squad = new List<CardInstance>(); int cost = 0;
            for (int i = 0; i < candidates.Length; i++) if ((mask & (1 << i)) != 0)
            { squad.Add(candidates[i]); cost += EnergyNeed(candidates[i]); }
            if (cost > energy) continue;
            double score;
            if (targets.Length == 0)
            {
                int direct = squad.Sum(c => AttackAt(c, BattlePosition.Attack) * Math.Max(1, Uses(c)));
                score = direct - cost * 40 + (direct >= view.Players[1 - player].Health ? 6000 : 0);
            }
            else
            {
                score = 0;
                foreach (CardInstance target in targets)
                {
                    if (squad.Any(c => !FavorableHit(c, target))) continue;
                    int damage = squad.Sum(c => Damage(c, target, target.BattlePosition) * Math.Max(1, Uses(c)));
                    if (damage <= 0) continue;
                    bool knockout = damage >= target.CurrentHealth;
                    double pressure = Math.Min(target.CurrentHealth, damage);
                    if (damage * 2 < target.CurrentHealth) pressure *= 0.35;
                    if (knockout) pressure += target.CurrentHealth * 0.7 + target.CurrentAttack * 0.35
                        + target.AllocatedEnergy * 100 + target.Definition.TributeCount * 220;
                    long removed = knockout ? target.InstanceId : 0;
                    pressure -= squad.Select(c => ExposurePenalty(c, IncomingExposure(c, removed))).DefaultIfEmpty(0).Max() * 0.25;
                    pressure -= cost * 40;
                    score = Math.Max(score, pressure);
                }
            }
            if (score > bestScore + 0.000001 || Math.Abs(score - bestScore) < 0.000001 && squad.Count < best.Count)
            { bestScore = score; best = new HashSet<long>(squad.Select(c => c.InstanceId)); }
        }
        if (best.Count == 0) best = SequentialFallbackPlan(energy, out bestScore);
        LastPlannedAttackers = best.Count; LastAttackPlanScore = bestScore;
        return best;
    }
    private bool MustDefend(CardInstance c, int energy) => !AttackPreparationOpportunity(c) || EnergyNeed(c) > energy;
    private GameAction Preparation(BotGameStateView state, IReadOnlyList<GameAction> legal)
    {
        if (state.CurrentPhase != TurnPhase.Main || !canBattle) return null;
        int energy = state.Players[player].AvailableEnergy;
        HashSet<long> plan = JointAttackPlan(energy);
        int plannedEnergy = plan.Where(id => Find(id) != null).Sum(id => EnergyNeed(Find(id)));
        foreach (long id in attackCommitments.Where(id => !plan.Contains(id)).ToArray())
        {
            CardInstance committed = Find(id);
            int needed = committed == null ? int.MaxValue : EnergyNeed(committed);
            if (committed != null && AttackPreparationOpportunity(committed) && !ShouldDefend(committed, energy)
                && plannedEnergy + needed <= energy)
            { plan.Add(id); plannedEnergy += needed; }
            else { attackCommitments.Remove(id); LastCancelledCommitments++; }
        }
        if (plan.Count == 0)
        {
            LastAttackPlanFallback = true;
            CardInstance fallback = Board(player)
                .OrderByDescending(c => attackCommitments.Contains(c.InstanceId)).ThenBy(c => EnergyNeed(c)).ThenBy(c => c.InstanceId)
                .FirstOrDefault(c => !reserved.Contains(c.InstanceId) && AttackOpportunity(c) && EnergyNeed(c) <= energy
                    && (c.BattlePosition == BattlePosition.Attack || CanTurn(c))
                    && (attackCommitments.Contains(c.InstanceId) || !LegacyShouldDefend(c, energy)));
            if (fallback != null) plan.Add(fallback.InstanceId);
        }
        LastPlannedAttackers = plan.Count;
        foreach (CardInstance c in Board(player).Where(c => plan.Contains(c.InstanceId))
            .OrderByDescending(c => sequentialOpeners.Contains(c.InstanceId))
            .ThenByDescending(c => attackCommitments.Contains(c.InstanceId)).ThenBy(c => EnergyNeed(c)).ThenBy(c => c.InstanceId))
        {
            if (reserved.Contains(c.InstanceId) || !AttackPreparationOpportunity(c) || EnergyNeed(c) > energy) continue;
            if (c.BattlePosition == BattlePosition.Defense && !CanTurn(c)) continue;
            int needed = EnergyNeed(c);
            GameAction step = needed > 0
                ? legal.FirstOrDefault(a => a.Type == GameActionType.AllocateEnergy && a.SourceInstanceId == c.InstanceId && a.Amount == needed)
                : c.BattlePosition == BattlePosition.Defense
                    ? legal.FirstOrDefault(a => a.Type == GameActionType.ChangeBattlePosition && a.SourceInstanceId == c.InstanceId && a.BattlePosition == BattlePosition.Attack) : null;
            if (step != null) return step;
        }
        return null;
    }
    private bool ReadyAttack() => canBattle && Board(player).Any(c => c.BattlePosition == BattlePosition.Attack && EnergyNeed(c) == 0
        && AttackPreparationOpportunity(c));
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
        GameAction preparation = Preparation(state, legal);
        bool fixPosture = legal.Any(a => a.Type == GameActionType.ChangeBattlePosition && a.BattlePosition == BattlePosition.Defense && MustDefend(Find(a.SourceInstanceId), energy));
        bool goodAttack = legal.Any(a => a.Type == GameActionType.Attack && GoodAttack(a, energy));
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
                || (!EnergyFuel(c) && (!AttackPreparationOpportunity(c) || c.AllocatedEnergy + a.Amount > c.Definition.AttackEnergyRequirement)))) continue;
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
        CardInstance source = Find(a.SourceInstanceId);
        bool attackAllocation = a.Type == GameActionType.AllocateEnergy && source != null
            && AttackPreparationOpportunity(source)
            && source.AllocatedEnergy < source.Definition.AttackEnergyRequirement
            && source.AllocatedEnergy + a.Amount <= source.Definition.AttackEnergyRequirement;
        if (a.Type == GameActionType.AllocateEnergy) invested.Add(a.SourceInstanceId);
        if (a.Type == GameActionType.SummonMonster && a.BattlePosition == BattlePosition.Attack ||
            a.Type == GameActionType.ChangeBattlePosition && a.BattlePosition == BattlePosition.Attack || attackAllocation)
            attackCommitments.Add(a.SourceInstanceId);
        if (a.Type == GameActionType.Attack) { invested.Remove(a.SourceInstanceId); attackCommitments.Remove(a.SourceInstanceId); }
        if (a.Type == GameActionType.ChangeBattlePosition && a.BattlePosition == BattlePosition.Defense) attackCommitments.Remove(a.SourceInstanceId);
    }
}
