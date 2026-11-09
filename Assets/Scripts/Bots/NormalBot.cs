using System;
using System.Collections.Generic;

// Tactical policy. Observe copies only owned/public information; no mutable state
// or hidden opponent definitions are retained or used in combat forecasts.
public sealed class NormalBot : IBot
{
    private sealed class Detail
    {
        public CardDefinition Definition;
        public int RemainingAttacks;
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
    public string DisplayName => "Normal Bot";

    public void Observe(GameState state, int player)
    {
        details.Clear(); publicGraveyard.Clear(); penalties.Clear();
        pendingEffect = state.PendingTriggeredEffect?.Effect;
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
        details[card.InstanceId] = new Detail { Definition = card.Definition,
            RemainingAttacks = Math.Max(0, card.MaxAttacksThisTurn - card.AttacksUsedThisTurn) };
    }

    public GameAction ChooseAction(BotGameStateView state, IReadOnlyList<GameAction> legalActions)
    {
        if (state == null || legalActions == null) throw new ArgumentNullException();
        GameAction best = null;
        double bestScore = double.NegativeInfinity;
        foreach (GameAction action in legalActions)
        {
            double score = Score(state, action);
            if (score > bestScore) { bestScore = score; best = action; }
        }
        return best;
    }

    private double Score(BotGameStateView state, GameAction action)
    {
        BotCardView card = Find(state, action.SourceInstanceId);
        BotPlayerView own = state.Players[action.PlayerIndex];
        BotPlayerView enemy = state.Players[1 - action.PlayerIndex];
        switch (action.Type)
        {
            case GameActionType.Attack: return AttackScore(state, action);
            case GameActionType.SummonMonster:
                double summon = Value(card) * 0.35 + (canBattle ? card.CurrentAttack * 0.12 : 0);
                foreach (long id in action.TributeInstanceIds) summon -= Value(Find(state, id)) * 0.55;
                int threat = StrongestAttack(enemy);
                summon += action.BattlePosition == BattlePosition.Defense
                    ? Math.Min(card.CurrentDefense, threat) * 0.18
                    : (canBattle && card.AttackEnergyRequirement <= own.AvailableEnergy ? 180 : 0);
                return summon;
            case GameActionType.AllocateEnergy:
                int needed = card.AttackEnergyRequirement - card.AllocatedEnergy;
                if (!canBattle || needed <= 0 || action.Amount > needed) return -100;
                double potential = AttackPotential(card, enemy);
                return potential <= 0 ? -100 : potential * (action.Amount == needed ? 0.8 : 0.15)
                    - action.Amount * 35;
            case GameActionType.ChangeBattlePosition:
                if (action.BattlePosition == BattlePosition.Attack)
                    return canBattle && card.AllocatedEnergy >= card.AttackEnergyRequirement && Remaining(card) > 0
                        ? AttackPotential(card, enemy) * 0.8 : -50;
                return Math.Min(card.CurrentDefense, StrongestAttack(enemy)) * 0.2
                    - (canBattle && Remaining(card) > 0 && card.AllocatedEnergy >= card.AttackEnergyRequirement
                        ? AttackPotential(card, enemy) : 0) - 30;
            case GameActionType.ActivateCard:
            case GameActionType.ActivateNormalSpell:
                return Activation(state, action, card);
            case GameActionType.ResolveTriggeredEffect:
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
                    case EffectCostType.AvailableEnergy:
                    case EffectCostType.RemoveAllocatedEnergy: score -= cost.Amount * 80; break;
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
        BotCardView target = Find(state, targetId);
        BotPlayerView own = state.Players[action.PlayerIndex];
        BotPlayerView enemy = state.Players[1 - action.PlayerIndex];
        double value = effect.ContinuousModifiers.Count > 0 ? 180 : 0;
        foreach (EffectOperationDefinition op in effect.Operations)
        {
            switch (op.Type)
            {
                case EffectOperationType.DrawCards: value += Math.Min(op.Amount, own.DeckCount) * 300; break;
                case EffectOperationType.SkipNextOpponentDrawStep: value += 300; break;
                case EffectOperationType.RestoreMonsterHealth:
                    if (target != null) value += Math.Min(op.Amount, target.MaximumHealth - target.CurrentHealth) * 0.5;
                    break;
                case EffectOperationType.RestoreHealthPerRemovedAllocatedEnergy:
                    int removed = 0;
                    foreach (EffectCostDefinition cost in effect.Costs)
                        if (cost.Type == EffectCostType.RemoveAllocatedEnergy) removed += cost.Amount;
                    if (target != null) value += Math.Min(op.Amount * removed,
                        target.MaximumHealth - target.CurrentHealth) * 0.5;
                    break;
                case EffectOperationType.IncreaseAttackUntilEndOfTurn:
                case EffectOperationType.IncreaseAttackByTriggerValue:
                    if (target != null && canBattle && Remaining(target) > 0 &&
                        target.AllocatedEnergy >= target.AttackEnergyRequirement)
                        value += op.Amount * 0.45 + KillUpgrade(target, enemy, op.Amount);
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
                    if (target != null && target.BattlePosition == BattlePosition.Defense)
                        value += Math.Min(op.Amount, StrongestAttack(enemy)) * (targetId == threatenedId ? 0.8 : 0.25);
                    break;
                case EffectOperationType.IncreaseMaximumHealth: value += op.Amount * 0.3; break;
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
        double gain = Math.Min(hp[index], damage) * 0.2;
        hp[index] -= damage;
        if (hp[index] <= 0)
        {
            int penalty = penalties.TryGetValue(target.MonsterRank, out int amount) ? amount : 0;
            lp -= penalty;
            gain += penalty + Value(target) * 0.4;
        }
        return gain;
    }

    private double AttackPotential(BotCardView card, BotPlayerView enemy)
    {
        double best = 0; bool any = false;
        foreach (BotCardView target in enemy.MonsterZones)
        {
            if (target == null) continue;
            any = true;
            int damage = Damage(card.CurrentAttack, target);
            best = Math.Max(best, Math.Min(damage, target.CurrentHealth) * 0.3 +
                (damage >= target.CurrentHealth ? Value(target) * 0.4 + 300 : 0));
        }
        return any ? best : card.CurrentAttack;
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
