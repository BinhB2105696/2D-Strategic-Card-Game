using System;
using System.Collections.Generic;

// Tactical policy. Observe copies only owned/public information; no mutable state
// or hidden opponent definitions are retained or used in combat forecasts.
public sealed class NormalBot : IBot, IStateObservingBot
{
    private sealed class Detail
    {
        public CardDefinition Definition;
        public int RemainingAttacks;
        public bool CanChangePosition;
        public CardInstance Projection;
    }

    private readonly Dictionary<long, Detail> details = new Dictionary<long, Detail>();
    private readonly Dictionary<long, BotCardView> publicGraveyard = new Dictionary<long, BotCardView>();
    private readonly Dictionary<MonsterRank, int> penalties = new Dictionary<MonsterRank, int>();
    private EffectDefinition pendingEffect;
    private long threatenedId;
    private int threatenedDamage;
    private bool canBattle, hostileLink;
    private EffectActivationWindow activationWindow;
    private int searchNodes;
    private int summonEnergyCost;
    public string DisplayName => "Normal Bot";

    public void Observe(GameState state, int player)
    {
        details.Clear(); publicGraveyard.Clear(); penalties.Clear();
        pendingEffect = state.PendingTriggeredEffect?.Effect;
        summonEnergyCost = state.Config.NormalSummonEnergyCost;
        canBattle = state.TurnNumber != 1 && !state.Players[player].IsBattlePhaseSkipScheduled(state.TurnNumber);
        threatenedId = 0; threatenedDamage = 0; hostileLink = false;
        foreach (PlayerState owner in state.Players)
        {
            if (owner.PlayerIndex == player)
                foreach (CardInstance card in owner.Hand) Remember(card);
            foreach (CardInstance card in owner.MonsterZones)
                if (card != null && (owner.PlayerIndex == player || !card.IsFaceDown)) Remember(card);
            foreach (CardInstance card in owner.SpellTrapZones)
                if (card != null && (owner.PlayerIndex == player || !card.IsFaceDown)) Remember(card);
            if (owner.FieldSpell != null) Remember(owner.FieldSpell);
            foreach (CardInstance card in owner.Graveyard)
            {
                Remember(card);
                publicGraveyard[card.InstanceId] = new BotCardView(card);
            }
        }
        foreach (CardInstance choice in CardExpansion.OwnChoiceCards(state, player)) Remember(choice);
        foreach (Detail detail in details.Values)
            penalties[detail.Definition.MonsterRank] = state.Config.GetKnockoutPenalty(detail.Definition.MonsterRank);
        ChainState chain = state.ActiveChain;
        activationWindow = chain == null ? EffectActivationWindow.OpenGameState
            : chain.Links.Count > 0 || chain.WindowType == ResponseWindowType.CardActivation
                ? EffectActivationWindow.ResponseToActivation : EffectActivationWindow.ResponseToAttackDeclaration;
        if (chain == null) return;
        if (chain.Links.Count > 0)
        {
            ChainLink last = chain.Links[chain.Links.Count - 1];
            hostileLink = last.PlayerIndex != player && !last.IsNegated;
        }
        PendingAttack attack = chain.PendingAttack;
        if (attack != null && attack.PlayerIndex != player)
        {
            threatenedId = attack.TargetInstanceId;
            CardInstance attacker = state.FindCard(attack.AttackerInstanceId);
            threatenedDamage = attacker != null ? attacker.CurrentAttack : 0;
        }
    }

    private void Remember(CardInstance card)
    {
        // Isolated public/owned projection for the pure continuous-modifier API.
        var projection = new CardInstance(card.InstanceId, card.OwnerPlayerIndex, card.Definition);
        projection.MoveToZone(card.Zone, card.IsFaceDown);
        if (card.Definition.IsMonster)
        {
            projection.ModifyMaximumHealth(card.MaximumHealth - projection.MaximumHealth);
            projection.ReceiveDamage(Math.Max(0, projection.MaximumHealth - card.CurrentHealth));
        }
        projection.ModifyAttack(card.CurrentAttack - projection.CurrentAttack);
        projection.ModifyDefense(card.CurrentDefense - projection.CurrentDefense);
        projection.AllocateEnergy(card.AllocatedEnergy);
        if (card.Zone == CardZone.MonsterZone) projection.SetBattlePosition(card.BattlePosition);
        details[card.InstanceId] = new Detail { Definition = card.Definition, Projection = projection,
            RemainingAttacks = Math.Max(0, card.MaxAttacksThisTurn - card.AttacksUsedThisTurn),
            CanChangePosition = !card.HasChangedPositionThisTurn && !card.WasSummonedThisTurn };
    }

    public GameAction ChooseAction(BotGameStateView state, IReadOnlyList<GameAction> legalActions)
    {
        if (state == null || legalActions == null) throw new ArgumentNullException();
        GameAction best = null;
        double bestScore = double.NegativeInfinity;
        foreach (GameAction action in legalActions)
        {
            double score = Score(state, action);
            if (score > bestScore || (score == bestScore && Prefer(state, action, best)))
            { bestScore = score; best = action; }
        }
        return best;
    }

    internal double ScoreForSearch(BotGameStateView state, GameAction action) => Score(state, action);

    private double Score(BotGameStateView state, GameAction action)
    {
        BotCardView card = Find(state, action.SourceInstanceId);
        BotPlayerView own = state.Players[action.PlayerIndex];
        BotPlayerView enemy = state.Players[1 - action.PlayerIndex];
        switch (action.Type)
        {
            case GameActionType.Attack: return AttackScore(state, action);
            case GameActionType.SummonMonster:
                return SummonScore(state, action, card);
            case GameActionType.AllocateEnergy:
                int needed = card.AttackEnergyRequirement - card.AllocatedEnergy;
                if (!CanPrepareAttack(card, own.AvailableEnergy) || needed <= 0 || action.Amount > needed) return -100;
                double potential = AttackPotential(card, enemy);
                return potential <= 0 ? -100 : potential * (action.Amount == needed ? 0.8 : 0.15)
                    - EnergyPrice(state, action, action.Amount);
            case GameActionType.ChangeBattlePosition:
                if (action.BattlePosition == BattlePosition.Attack)
                    return CanPrepareAttack(card, own.AvailableEnergy)
                        ? AttackPotential(card, enemy) * (card.AllocatedEnergy >= card.AttackEnergyRequirement ? 0.9 : 0.2) : -100;
                return CanPrepareAttack(card, own.AvailableEnergy) && AttackPotential(card, enemy) > 0
                    ? -100 : 100 + Math.Min(card.CurrentDefense, StrongestAttack(enemy)) * 0.2;
            case GameActionType.ActivateCard:
            case GameActionType.ActivateNormalSpell:
                return Activation(state, action, card);
            case GameActionType.ResolveTriggeredEffect:
                if (pendingEffect != null && pendingEffect.Trigger == EffectTriggerType.EndPhase &&
                    card != null && card.Category == CardCategory.Spell && card.SpellType == SpellType.Field &&
                    details.TryGetValue(card.InstanceId, out Detail fieldDetail))
                {
                    double upkeepValue = 0;
                    foreach (EffectDefinition fieldEffect in fieldDetail.Definition.Effects)
                        upkeepValue += ContinuousValue(state, card, null, fieldEffect,
                            own.AvailableEnergy, action.PlayerIndex);
                    int upkeepCost = 0;
                    foreach (EffectCostDefinition cost in pendingEffect.Costs) upkeepCost += cost.Amount;
                    return upkeepValue - EnergyPrice(state, action, upkeepCost);
                }
                return pendingEffect == null ? 1 : EffectValue(state, action, pendingEffect);
            case GameActionType.SetTrap: return 80 + (card == null ? 0 : (int)card.Speed * 10);
            case GameActionType.AdvancePhase: return 1;
            case GameActionType.EnterEndPhase:
            case GameActionType.EndTurn:
            case GameActionType.PassResponse:
            case GameActionType.DeclineTriggeredEffect: return 0;
            default: return -1;
        }
    }

    private double Activation(BotGameStateView state, GameAction action, BotCardView card)
    {
        if (card == null || !details.TryGetValue(card.InstanceId, out Detail detail)) return -1;
        foreach (EffectDefinition effect in detail.Definition.Effects)
        {
            bool matches = state.HasOpenResponseWindow
                ? effect.Trigger == EffectTriggerType.OnAttackDeclared || effect.Trigger == EffectTriggerType.OnSpellTrapActivated
                : effect.Trigger == EffectTriggerType.ManualActivation && effect.ActivationWindow == EffectActivationWindow.OpenGameState;
            if (!matches || effect.ActivationWindow != activationWindow) continue;
            double score = EffectValue(state, action, effect) - (card.Category == CardCategory.Monster ? 0 : 100);
            foreach (EffectCostDefinition cost in effect.Costs)
            {
                switch (cost.Type)
                {
                    case EffectCostType.PlayerHealth:
                        score -= cost.Amount * (state.Players[action.PlayerIndex].Health <= cost.Amount * 2 ? 2 : 0.25); break;
                    case EffectCostType.AvailableEnergy: score -= EnergyPrice(state, action, cost.Amount); break;
                    case EffectCostType.RemoveAllocatedEnergy:
                        score -= cost.Amount * 20;
                        if (canBattle && Remaining(card) > 0 && card.AllocatedEnergy >= card.AttackEnergyRequirement &&
                            card.AllocatedEnergy - cost.Amount < card.AttackEnergyRequirement)
                            score -= AttackPotential(card, state.Players[1 - action.PlayerIndex]) * 0.8;
                        break;
                    case EffectCostType.DiscardFromHand: score -= cost.Amount * 350; break;
                    case EffectCostType.TributeMonster: score -= cost.Amount * 700; break;
                }
            }
            return score;
        }
        return -1;
    }

    private double EffectValue(BotGameStateView state, GameAction action, EffectDefinition effect)
    {
        long targetId = action.TargetInstanceIds.Count > 0 ? action.TargetInstanceIds[0] : action.TargetInstanceId;
        BotCardView source = Find(state, action.SourceInstanceId);
        BotCardView target = targetId != 0 ? Find(state, targetId) :
            effect.TargetType == EffectTargetType.None && source != null && source.Category == CardCategory.Monster ? source : null;
        BotPlayerView own = state.Players[action.PlayerIndex];
        BotPlayerView enemy = state.Players[1 - action.PlayerIndex];
        int energy = own.AvailableEnergy;
        foreach (EffectCostDefinition cost in effect.Costs)
            if (cost.Type == EffectCostType.AvailableEnergy ||
                (cost.Type == EffectCostType.EnergyWithLpDeficitDiscount && state.MainStartLpDeficit < cost.SecondaryAmount)) energy -= cost.Amount;
        double value = ContinuousValue(state, source, target, effect, energy, action.PlayerIndex);
        foreach (EffectOperationDefinition op in effect.Operations)
        {
            switch (op.Type)
            {
                case EffectOperationType.DrawCards:
                    value += Math.Min(op.Amount, own.DeckCount) * (own.HandCount >= 6 ? 140 : 280); break;
                case EffectOperationType.RestoreAvailableEnergy:
                case EffectOperationType.RestoreEnergyWithLpBonus:
                    value += Math.Min(own.MaxEnergy - own.AvailableEnergy, op.Amount +
                        (op.Type == EffectOperationType.RestoreEnergyWithLpBonus && own.Health < enemy.Health ? op.SecondaryAmount : 0)) * 140; break;
                case EffectOperationType.ReallocateEnergyToPlayer:
                    value += Math.Min(own.MaxEnergy - own.AvailableEnergy, op.Amount) * 140; break;
                case EffectOperationType.SearchSelectedCard:
                    value += target == null ? 0 : 280 + Value(target) * .15; break;
                case EffectOperationType.RecoverSelectedCard:
                    value += target == null ? 0 : 180 + Value(target) * .2; break;
                case EffectOperationType.RemoveSelectedSupport:
                    value += target == null ? 0 : 350; break;
                case EffectOperationType.ChangeSelectedMonsterToAttack:
                    value += target == null ? 0 : target.CurrentDefense * .6 + target.AttackPriority * 400; break;
                case EffectOperationType.AllocateSelectedEnergy:
                    value += target == null ? 0 : Math.Min(op.Amount, Math.Max(0, target.AttackEnergyRequirement - target.AllocatedEnergy)) * 180 + 20; break;
                case EffectOperationType.RemoveSelectedEnergy:
                    value += target == null ? 0 : Math.Min(op.Amount, target.AllocatedEnergy) * 150; break;
                case EffectOperationType.SwitchToDefenseAfterBattle:
                    value += source == null ? 0 : source.CurrentDefense * .4 - source.AllocatedEnergy * 30; break;
                case EffectOperationType.SkipNextOpponentDrawStep: value += 300; break;
                case EffectOperationType.RestoreMonsterHealth:
                    if (target != null) value += Math.Min(op.Amount, target.MaximumHealth - target.CurrentHealth) * 0.5;
                    break;
                case EffectOperationType.RestoreHealthPerRemovedAllocatedEnergy:
                    int removed = source != null ? source.AllocatedEnergy : 0;
                    if (target != null) value += Math.Min(op.Amount * removed,
                        target.MaximumHealth - target.CurrentHealth) * 0.5 - removed * 30;
                    break;
                case EffectOperationType.IncreaseAttackUntilEndOfTurn:
                case EffectOperationType.IncreaseAttackByTriggerValue:
                    if (target != null) value += AttackBonusValue(target, enemy, op.Amount, energy, false);
                    break;
                case EffectOperationType.TransferAttackUntilEndOfTurn:
                    if (target != null && action.TargetInstanceIds.Count > 1)
                    {
                        BotCardView receiver = Find(state, action.TargetInstanceIds[1]);
                        if (receiver != null && canBattle && Remaining(receiver) > 0 &&
                            receiver.AllocatedEnergy >= receiver.AttackEnergyRequirement)
                            value += KillUpgrade(receiver, enemy, target.CurrentAttack) + target.CurrentAttack *
                                (Remaining(target) == 0 ? 0.35 : 0.05);
                    }
                    break;
                case EffectOperationType.ReduceAttackUntilEndOfTurn:
                    if (target != null && state.HasOpenResponseWindow) value += Math.Min(op.Amount, target.CurrentAttack) * 0.6;
                    break;
                case EffectOperationType.IncreaseDefensePermanently:
                case EffectOperationType.IncreaseDefenseUntilEndOfTurn:
                    if (target != null) value += SurvivalValue(target, enemy, 0, op.Amount,
                        op.Type == EffectOperationType.IncreaseDefensePermanently);
                    break;
                case EffectOperationType.IncreaseMaximumHealth:
                    if (target != null) value += SurvivalValue(target, enemy, op.Amount, 0, true);
                    break;
                case EffectOperationType.ReduceMaximumHealth:
                    if (target != null) value += op.Amount * 0.5 + (op.Amount >= target.CurrentHealth ? Value(target) * 0.5 : 0);
                    break;
                case EffectOperationType.NegatePreviousChainLink: if (hostileLink) value += 650; break;
                case EffectOperationType.ProtectAttackedMonster:
                case EffectOperationType.SkipCurrentBattlePhase:
                    if (threatenedDamage > 0)
                        value += threatenedId == 0 ? Math.Min(threatenedDamage, own.Health) :
                            Value(Find(state, threatenedId)) * 0.6;
                    break;
                case EffectOperationType.SearchBasicMonsterFromDeck: value += own.DeckCount > 0 ? 350 : 0; break;
                case EffectOperationType.RecoverBasicMonsterFromGraveyard: value += target == null ? 0 : Value(target) * 0.35; break;
            }
        }
        return value;
    }

    private bool CanPrepareAttack(BotCardView card, int energy)
    {
        if (card == null || !canBattle || Remaining(card) <= 0 ||
            Math.Max(0, card.AttackEnergyRequirement - card.AllocatedEnergy) > energy) return false;
        return card.BattlePosition != BattlePosition.Defense ||
            (details.TryGetValue(card.InstanceId, out Detail detail) && detail.CanChangePosition);
    }

    private double EnergyPrice(BotGameStateView state, GameAction action, int amount)
    {
        BotPlayerView own = state.Players[action.PlayerIndex], enemy = state.Players[1 - action.PlayerIndex];
        double opportunity = 0;
        foreach (BotCardView monster in own.MonsterZones)
        {
            if (monster == null || (action.Type == GameActionType.AllocateEnergy && monster.InstanceId == action.SourceInstanceId)) continue;
            int needed = Math.Max(0, monster.AttackEnergyRequirement - monster.AllocatedEnergy);
            if (needed > 0 && needed > own.AvailableEnergy - amount && CanPrepareAttack(monster, own.AvailableEnergy))
                opportunity = Math.Max(opportunity, AttackPotential(monster, enemy) * 0.35);
        }
        return amount * (own.AvailableEnergy - amount <= 1 ? 30 : 12) + opportunity;
    }

    private double SummonScore(BotGameStateView state, GameAction action, BotCardView card)
    {
        BotPlayerView own = state.Players[action.PlayerIndex], enemy = state.Players[1 - action.PlayerIndex];
        int tributeAttack = 0, bonus = 0;
        double lost = 0, lostPressure = 0;
        foreach (long id in action.TributeInstanceIds)
        {
            BotCardView tribute = Find(state, id);
            lost += Value(tribute) * 0.45;
            if (CanPrepareAttack(tribute, own.AvailableEnergy)) lostPressure += AttackPotential(tribute, enemy) * 0.35;
            if (details.TryGetValue(id, out Detail detail)) tributeAttack += detail.Definition.BaseAttack;
        }
        if (details.TryGetValue(card.InstanceId, out Detail summoned))
            foreach (EffectDefinition effect in summoned.Definition.Effects)
                if (effect.Trigger == EffectTriggerType.OnSummoned)
                    foreach (EffectOperationDefinition op in effect.Operations)
                        if (op.Type == EffectOperationType.IncreaseAttackByTriggerValue) bonus += tributeAttack;
        bool ready = canBattle && own.AvailableEnergy - summonEnergyCost >= card.AttackEnergyRequirement;
        double score = (Value(card) + bonus * 0.7) * 0.45 - lost - lostPressure;
        if (ready) score += Potential(card, enemy, card.CurrentAttack + bonus) * 0.4;
        if (action.BattlePosition == BattlePosition.Defense)
            score += ready ? -100 : 180 + Math.Min(card.CurrentDefense, StrongestAttack(enemy)) * 0.2;
        else score += ready ? 180 : -220;
        return score;
    }

    private double AttackBonusValue(BotCardView target, BotPlayerView enemy, int amount, int energy, bool permanent)
    {
        if (amount <= 0) return 0;
        double value = permanent ? amount * 0.2 : 0;
        if (CanPrepareAttack(target, energy))
            value += Math.Max(0, Potential(target, enemy, target.CurrentAttack + amount) - AttackPotential(target, enemy)) * 0.8
                + amount * 0.2 * Math.Max(1, Remaining(target)) + KillUpgrade(target, enemy, amount);
        return value;
    }

    private double SurvivalValue(BotCardView target, BotPlayerView enemy, int health, int defense, bool permanent)
    {
        if (health <= 0 && defense <= 0) return 0;
        bool defending = target.BattlePosition == BattlePosition.Defense;
        double value = health * 0.22 + defense * (defending ? 0.22 : permanent ? 0.06 : 0);
        foreach (BotCardView attacker in enemy.MonsterZones)
        {
            if (attacker == null) continue;
            int before = Damage(attacker.CurrentAttack, target);
            int after = defending ? Math.Max(0, before - defense) : before;
            if (before >= target.CurrentHealth && after < target.CurrentHealth + health)
                value = Math.Max(value, Value(target) * 0.55 + KnockoutPenalty(target) * 0.5);
        }
        if (target.InstanceId == threatenedId && threatenedDamage > 0)
        {
            int after = Math.Max(0, threatenedDamage - (defending ? target.CurrentDefense + defense : 0));
            if (after < target.CurrentHealth + health) value += Value(target) * 0.5;
        }
        return value;
    }

    private double ContinuousValue(BotGameStateView state, BotCardView source, BotCardView target,
        EffectDefinition effect, int energy, int player)
    {
        if (source == null || effect.ContinuousModifiers.Count == 0 || !details.TryGetValue(source.InstanceId, out Detail sourceDetail)) return 0;
        double value = 0;
        BotPlayerView own = state.Players[player], enemy = state.Players[1 - player];
        foreach (BotCardView monster in own.MonsterZones)
        {
            if (monster == null || (target != null && monster.InstanceId != target.InstanceId) ||
                !details.TryGetValue(monster.InstanceId, out Detail targetDetail)) continue;
            foreach (IContinuousModifier modifier in effect.ContinuousModifiers)
            {
                int attack = modifier.ModifyAttack(sourceDetail.Projection, targetDetail.Projection);
                int defense = modifier.ModifyDefense(sourceDetail.Projection, targetDetail.Projection);
                int health = modifier.ModifyHealth(sourceDetail.Projection, targetDetail.Projection);
                value += AttackBonusValue(monster, enemy, attack, energy, true)
                    + SurvivalValue(monster, enemy, health, defense, true);
            }
        }
        return value;
    }

    private bool Prefer(BotGameStateView state, GameAction action, GameAction best)
    {
        if (best == null) return true;
        bool ending = action.Type == GameActionType.EnterEndPhase || action.Type == GameActionType.PassResponse;
        bool bestEnding = best.Type == GameActionType.EnterEndPhase || best.Type == GameActionType.PassResponse;
        if (ending != bestEnding) return ending;
        if (action.Type == GameActionType.Attack && best.Type == GameActionType.Attack)
        {
            int attack = Find(state, action.SourceInstanceId).CurrentAttack;
            int bestAttack = Find(state, best.SourceInstanceId).CurrentAttack;
            if (attack != bestAttack) return attack < bestAttack;
        }
        if (action.Type != best.Type) return (int)action.Type < (int)best.Type;
        if (action.Amount != best.Amount) return action.Amount < best.Amount;
        if (action.SourceInstanceId != best.SourceInstanceId) return action.SourceInstanceId < best.SourceInstanceId;
        if (action.TargetInstanceId != best.TargetInstanceId) return action.TargetInstanceId < best.TargetInstanceId;
        return action.ZoneIndex < best.ZoneIndex;
    }

    private int KnockoutPenalty(BotCardView card) => penalties.TryGetValue(card.MonsterRank, out int amount) ? amount : 0;

    // Bounded public-board forecast: sequence damage/KO/direct attacks only.
    // Real chains/effects are resolved by GameRules; replan after every action.
    private double AttackScore(BotGameStateView state, GameAction action)
    {
        BotPlayerView own = state.Players[action.PlayerIndex], enemy = state.Players[1 - action.PlayerIndex];
        var attackers = new List<BotCardView>();
        foreach (BotCardView card in own.MonsterZones)
            if (card != null && card.BattlePosition == BattlePosition.Attack && Remaining(card) > 0 &&
                card.AllocatedEnergy >= card.AttackEnergyRequirement) attackers.Add(card);
        var targets = new List<BotCardView>();
        foreach (BotCardView card in enemy.MonsterZones) if (card != null) targets.Add(card);
        int[] hp = new int[targets.Count], uses = new int[attackers.Count];
        for (int i = 0; i < hp.Length; i++) hp[i] = targets[i].CurrentHealth;
        for (int i = 0; i < uses.Length; i++) uses[i] = Remaining(attackers[i]);
        BotCardView source = Find(state, action.SourceInstanceId), target = Find(state, action.TargetInstanceId);
        int index = attackers.FindIndex(c => c.InstanceId == action.SourceInstanceId);
        if (index < 0) return -1;
        uses[index]--;
        int lp = enemy.Health;
        double gain;
        if (target == null) { lp -= source.CurrentAttack; gain = source.CurrentAttack; }
        else
        {
            int t = targets.FindIndex(c => c.InstanceId == target.InstanceId);
            if (t < 0) return -1;
            gain = Hit(source, target, hp, t, ref lp);
        }
        searchNodes = 0;
        return gain + Forecast(attackers, targets, uses, hp, lp, 5);
    }

    private double Forecast(List<BotCardView> attackers, List<BotCardView> targets,
        int[] uses, int[] hp, int lp, int depth)
    {
        if (lp <= 0) return 1000000;
        if (depth == 0 || ++searchNodes > 256) return 0;
        bool empty = true;
        foreach (int health in hp) if (health > 0) empty = false;
        double best = 0;
        for (int i = 0; i < attackers.Count; i++)
        {
            if (uses[i] <= 0) continue;
            uses[i]--;
            if (empty)
                best = Math.Max(best, attackers[i].CurrentAttack + Forecast(attackers, targets, uses, hp,
                    lp - attackers[i].CurrentAttack, depth - 1));
            else for (int j = 0; j < hp.Length; j++)
            {
                if (hp[j] <= 0) continue;
                int oldHp = hp[j], nextLp = lp;
                double gain = Hit(attackers[i], targets[j], hp, j, ref nextLp);
                best = Math.Max(best, gain + Forecast(attackers, targets, uses, hp, nextLp, depth - 1));
                hp[j] = oldHp;
            }
            uses[i]++;
        }
        return best;
    }

    private double Hit(BotCardView attacker, BotCardView target, int[] hp, int index, ref int lp)
    {
        int damage = Damage(attacker.CurrentAttack, target);
        double gain = Math.Min(hp[index], damage) * 0.25;
        hp[index] -= damage;
        if (hp[index] <= 0)
        {
            int penalty = KnockoutPenalty(target);
            lp -= penalty;
            gain += penalty * 1.2 + Value(target) * 0.4 + target.CurrentAttack * 0.55;
        }
        return gain;
    }

    private double AttackPotential(BotCardView card, BotPlayerView enemy)
        => Potential(card, enemy, card.CurrentAttack);

    private double Potential(BotCardView card, BotPlayerView enemy, int attack)
    {
        double best = 0; bool any = false;
        foreach (BotCardView target in enemy.MonsterZones)
        {
            if (target == null) continue;
            any = true;
            int damage = Damage(attack, target);
            best = Math.Max(best, Math.Min(damage, target.CurrentHealth) * 0.3 +
                (damage >= target.CurrentHealth ? Value(target) * 0.4 + KnockoutPenalty(target) + target.CurrentAttack * 0.4 : 0));
        }
        return any ? best : attack;
    }

    private double KillUpgrade(BotCardView card, BotPlayerView enemy, int bonus)
    {
        double best = 0;
        foreach (BotCardView target in enemy.MonsterZones)
            if (target != null && Damage(card.CurrentAttack, target) < target.CurrentHealth &&
                Damage(card.CurrentAttack + bonus, target) >= target.CurrentHealth)
                best = Math.Max(best, Value(target) * 0.5 + 400);
        return best;
    }

    private BotCardView Find(BotGameStateView state, long id) => state.FindCard(id) ??
        (publicGraveyard.TryGetValue(id, out BotCardView card) ? card : null);
    private int Remaining(BotCardView card) => details.TryGetValue(card.InstanceId, out Detail d) ? d.RemainingAttacks : 0;
    private static int Damage(int attack, BotCardView target) => target.BattlePosition == BattlePosition.Defense
        ? Math.Max(0, attack - target.CurrentDefense) : attack;
    private static int StrongestAttack(BotPlayerView player)
    {
        int best = 0;
        foreach (BotCardView card in player.MonsterZones) if (card != null) best = Math.Max(best, card.CurrentAttack);
        return best;
    }
    private static double Value(BotCardView card) => card == null ? 0 : card.CurrentHealth * 0.4 +
        card.CurrentAttack * 0.7 + card.CurrentDefense * 0.25 + card.AllocatedEnergy * 120 + 250;
}
