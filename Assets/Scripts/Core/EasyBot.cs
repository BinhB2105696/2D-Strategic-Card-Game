using System;
using System.Collections.Generic;

// Deterministic rule-based opponent. It scores only actions supplied by
// GetLegalActions; GameSession/GameRules remain the final authority.
public sealed class EasyBot : IBot
{
    public string DisplayName => "Easy Bot";

    public GameAction ChooseAction(
        BotGameStateView state,
        IReadOnlyList<GameAction> legalActions)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (legalActions == null)
            throw new ArgumentNullException(nameof(legalActions));
        if (legalActions.Count == 0)
            return null;

        GameAction bestAction = legalActions[0];
        int bestScore = ScoreAction(state, bestAction);
        for (int index = 1; index < legalActions.Count; index++)
        {
            GameAction candidate = legalActions[index];
            int score = ScoreAction(state, candidate);
            if (score > bestScore)
            {
                bestAction = candidate;
                bestScore = score;
            }
        }
        return bestAction;
    }

    private static int ScoreAction(
        BotGameStateView state,
        GameAction action)
    {
        switch (action.Type)
        {
            case GameActionType.ResolveTriggeredEffect:
                return 100000 + CardValue(
                    state.FindCard(action.TargetInstanceId));

            case GameActionType.DeclineTriggeredEffect:
                return 100;

            case GameActionType.PassResponse:
                return 1000;

            case GameActionType.ActivateCard:
            case GameActionType.ActivateNormalSpell:
                return ScoreActivation(state, action);

            case GameActionType.Attack:
                return ScoreAttack(state, action);

            case GameActionType.AllocateEnergy:
                return ScoreEnergyAllocation(state, action);

            case GameActionType.SummonMonster:
                return ScoreSummon(state, action);

            case GameActionType.SetTrap:
            {
                BotCardView trap = state.FindCard(action.SourceInstanceId);
                return 60000 + (trap == null ? 0 : (int)trap.Speed * 100);
            }

            case GameActionType.ChangeBattlePosition:
                return action.BattlePosition == BattlePosition.Attack
                    ? 50000
                    : -1000;

            case GameActionType.AdvancePhase:
                return HasReadyAttacker(
                    state.Players[action.PlayerIndex])
                    ? 30000
                    : 5000;

            case GameActionType.EnterEndPhase:
                return 2000;

            case GameActionType.EndTurn:
                return 1000;

            default:
                return 0;
        }
    }

    private static int ScoreActivation(
        BotGameStateView state,
        GameAction action)
    {
        BotCardView source = state.FindCard(action.SourceInstanceId);
        int score = state.HasOpenResponseWindow ? 90000 : 70000;
        if (source == null)
            return score;

        if (source.SpellType == SpellType.Field)
            score += 2000;
        else if (source.SpellType == SpellType.Equip)
            score += 1500;
        score += (int)source.Speed * 100;

        if (action.TargetInstanceIds.Count > 0)
        {
            BotCardView firstTarget = state.FindCard(
                action.TargetInstanceIds[0]);
            if (firstTarget != null)
            {
                score += firstTarget.OwnerPlayerIndex == action.PlayerIndex
                    ? CardValue(firstTarget) / 20
                    : firstTarget.AllocatedEnergy * 300 +
                      firstTarget.CurrentAttack / 10;
            }
        }

        // Power-transfer-style effects use ordered targets. Prefer moving
        // Attack from a stronger first target to a different second target.
        if (action.TargetInstanceIds.Count > 1)
        {
            BotCardView donor = state.FindCard(action.TargetInstanceIds[0]);
            if (donor != null)
                score += donor.CurrentAttack;
        }
        return score;
    }

    private static int ScoreAttack(
        BotGameStateView state,
        GameAction action)
    {
        BotCardView attacker = state.FindCard(action.SourceInstanceId);
        if (attacker == null)
            return 80000;

        if (action.TargetInstanceId == 0)
            return 90000 + attacker.CurrentAttack;

        BotCardView target = state.FindCard(action.TargetInstanceId);
        if (target == null)
            return 80000;

        int damage = target.BattlePosition == BattlePosition.Defense
            ? Math.Max(0, attacker.CurrentAttack - target.CurrentDefense)
            : attacker.CurrentAttack;
        int lethalBonus = damage >= target.CurrentHealth ? 20000 : 0;
        return 80000 + lethalBonus + damage - target.CurrentHealth / 10;
    }

    private static int ScoreEnergyAllocation(
        BotGameStateView state,
        GameAction action)
    {
        BotCardView monster = state.FindCard(action.SourceInstanceId);
        if (monster == null)
            return -2000;

        int needed = Math.Max(
            0,
            monster.AttackEnergyRequirement - monster.AllocatedEnergy);
        if (needed == 0 || action.Amount > needed)
            return -2000;

        int completionBonus = action.Amount == needed ? 5000 : 0;
        return 85000 + completionBonus + monster.CurrentAttack / 10;
    }

    private static int ScoreSummon(
        BotGameStateView state,
        GameAction action)
    {
        BotCardView monster = state.FindCard(action.SourceInstanceId);
        int score = 75000 + CardValue(monster) / 10;
        if (action.BattlePosition == BattlePosition.Attack)
            score += 1000;

        for (int index = 0; index < action.TributeInstanceIds.Count; index++)
            score -= CardValue(state.FindCard(
                action.TributeInstanceIds[index])) / 12;
        return score;
    }

    private static bool HasReadyAttacker(BotPlayerView player)
    {
        for (int index = 0; index < player.MonsterZones.Count; index++)
        {
            BotCardView monster = player.MonsterZones[index];
            if (monster != null &&
                monster.BattlePosition == BattlePosition.Attack &&
                monster.AllocatedEnergy >= monster.AttackEnergyRequirement)
            {
                return true;
            }
        }
        return false;
    }

    private static int CardValue(BotCardView card)
    {
        if (card == null)
            return 0;
        if (card.Category != CardCategory.Monster)
            return 500 + (int)card.Speed * 100;
        return card.CurrentHealth + card.CurrentAttack +
            card.CurrentDefense + card.AllocatedEnergy * 100;
    }
}
