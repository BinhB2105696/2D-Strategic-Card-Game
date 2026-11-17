using System;
using System.Collections.Generic;
using System.Linq;

// Hard-only estimates, not legality checks or promises about future draws.
// Callers supply public cards at the root, or cards from a sampled search world.
internal static class HardStrategicEvaluation
{
    internal static EffectOperationDefinition ProspectivePassive(CardInstance card, EffectOperationType type)
    {
        if (card == null) return null;
        if (card.Zone != CardZone.Hand) return CardExpansion.Passive(card, type);
        CardInstance projected = card.CopyForSearch();
        projected.MoveToZone(CardZone.MonsterZone, false);
        return CardExpansion.Passive(projected, type);
    }

    private static double BodyValue(CardInstance card) => 250 + card.CurrentHealth * 0.3
        + card.CurrentAttack * 0.4 + card.CurrentDefense * 0.12;

    private static double Survival(CardInstance card, CardInstance[] enemies, int enemyEnergy)
    {
        int worst = 0;
        foreach (CardInstance enemy in enemies)
        {
            if (enemy.Definition.AttackEnergyRequirement > enemyEnergy) continue;
            int defense = card.BattlePosition == BattlePosition.Defense ? card.CurrentDefense : 0;
            int pierce = ProspectivePassive(enemy, EffectOperationType.PierceDefense)?.Amount ?? 0;
            worst = Math.Max(worst, Math.Max(0, enemy.CurrentAttack - Math.Max(0, defense - pierce)));
        }
        // Enemy targeting, Energy sharing and hidden responses are still simulated by MCTS.
        if (worst >= card.CurrentHealth) return 0.35;
        return worst * 2 >= card.CurrentHealth ? 0.7 : 1;
    }

    internal static double DrawEngineValue(CardInstance card, int handCount, int deckCount, int energy)
    {
        if (!card.Definition.IsMonster || deckCount == 0) return 0;
        double best = 0;
        foreach (EffectDefinition effect in card.Definition.Effects)
        {
            if (effect.Trigger != EffectTriggerType.ManualActivation
                || effect.Kind != EffectKind.Activated
                || (effect.MaxUses > 0 && effect.Reset != EffectUsageReset.PerTurn)) continue;
            int draw = effect.Operations.Where(op => op.Type == EffectOperationType.DrawCards).Sum(op => op.Amount);
            if (draw == 0 || effect.Costs.Any(cost => cost.Type != EffectCostType.AvailableEnergy)) continue;
            int cost = effect.Costs.Sum(item => item.Amount);
            if (cost > energy) continue;
            // Two discounted future opportunities; value only the remaining count, never deck identity/order.
            double futureCards = Math.Min(deckCount, draw * 1.6);
            double scarcity = handCount <= 3 ? 1.2 : handCount >= 7 ? 0.6 : 1;
            double conditions = effect.Conditions.Count == 0 ? 1 : 0.5;
            best = Math.Max(best, futureCards * 220 * scarcity * conditions / (1 + cost * 0.18));
        }
        return best;
    }

    private static double FieldBonus(CardInstance card, int handCount, int deckCount, int energy,
        CardInstance[] enemies, int enemyEnergy)
    {
        if (card.IsFaceDown || card.IsDestroyed) return 0;
        double survival = Survival(card, enemies, enemyEnergy);
        return (card.Definition.TributeCount * 120
            + DrawEngineValue(card, handCount, deckCount, energy)) * survival;
    }

    internal static double PortfolioBonus(IEnumerable<CardInstance> handCards, IEnumerable<CardInstance> fieldCards,
        IEnumerable<CardInstance> enemyCards, int deckCount, int availableEnergy, int maxEnergy,
        int enemyEnergy, int summonCost, int zoneCount)
    {
        CardInstance[] hand = handCards.Where(c => c != null).ToArray();
        CardInstance[] board = fieldCards.Where(c => c != null && !c.IsDestroyed).ToArray();
        CardInstance[] enemies = enemyCards.Where(c => c != null && !c.IsFaceDown && !c.IsDestroyed).ToArray();
        CardInstance[] basics = hand.Where(c => c.Definition.IsMonster && c.Definition.TributeCount == 0)
            .OrderBy(BodyValue).ToArray();
        int slots = Math.Max(0, zoneCount - board.Length), budget = Math.Max(availableEnergy, maxEnergy);
        double value = board.Sum(c => FieldBonus(c, hand.Length, deckCount, budget, enemies, enemyEnergy));
        bool canBuild = board.Length > 0 || (slots > 0 && basics.Length > 0 && budget >= summonCost);
        if (slots > 0 && budget >= summonCost)
            value += Math.Min(slots, basics.Length) * (board.Length == 0 ? 150 : 60);
        foreach (CardInstance support in hand.Where(c => !c.Definition.IsMonster))
            if (!canBuild && support.Definition.Effects.Any(e => e.TargetType == EffectTargetType.OwnMonster
                || e.TargetType == EffectTargetType.OwnWoundedMonster || e.TargetType == EffectTargetType.OwnOtherMonster
                || e.TargetType == EffectTargetType.OwnMonsterWithEnergy)) value -= 90;

        // A single best upgrade package: several hand cards cannot reuse the same tributes/Energy for free.
        double bestPlan = 0;
        foreach (CardInstance upgrade in hand.Where(c => c.Definition.IsMonster && c.Definition.TributeCount > 0))
        {
            int need = upgrade.Definition.TributeCount;
            bool reachable = false;
            if (need <= zoneCount)
                foreach (CardInstance[] material in TributeSets(board, need))
                {
                    int missing = need - material.Length;
                    int cost = (missing + 1) * summonCost;
                    if (missing > basics.Length || missing > slots || cost > budget) continue;
                    reachable = true;
                    double lost = material.Sum(c => BodyValue(c)
                        + FieldBonus(c, hand.Length, deckCount, budget, enemies, enemyEnergy));
                    lost += basics.Take(missing).Sum(c => 180 + BodyValue(c) * 0.2);
                    double gain = BodyValue(upgrade) + need * 120
                        + DrawEngineValue(upgrade, hand.Length - missing - 1, deckCount, budget) - lost;
                    double reliability = material.Select(c => Survival(c, enemies, enemyEnergy)).DefaultIfEmpty(1).Min();
                    double readiness = cost <= availableEnergy ? 1 : 0.65;
                    bestPlan = Math.Max(bestPlan, Math.Max(0, gain) * 0.35 * reliability * readiness);
                }
            if (!reachable) value -= 90; // High-rank cards without a feasible material package are not ready power.
        }
        return value + bestPlan;
    }

    private static IEnumerable<CardInstance[]> TributeSets(CardInstance[] board, int limit, int index = 0)
    {
        if (index == board.Length) { yield return new CardInstance[0]; yield break; }
        foreach (CardInstance[] tail in TributeSets(board, limit, index + 1))
        {
            yield return tail;
            if (tail.Length < limit) yield return new[] { board[index] }.Concat(tail).ToArray();
        }
    }

    internal static double DrawActionBonus(CardInstance source, int handCount, int deckCount, bool needsRecovery)
    {
        double best = 0;
        foreach (EffectDefinition effect in source.Definition.Effects.Where(e => e.Trigger == EffectTriggerType.ManualActivation))
        {
            int draw = Math.Min(deckCount, effect.Operations.Where(op => op.Type == EffectOperationType.DrawCards).Sum(op => op.Amount));
            int consumed = source.Definition.Category == CardCategory.Spell && source.Definition.SpellType == SpellType.Normal ? 1 : 0;
            int net = draw - consumed - effect.Costs.Where(c => c.Type == EffectCostType.DiscardFromHand).Sum(c => c.Amount);
            best = Math.Max(best, Math.Max(0, net) * (handCount <= 3 ? 120 : 70) * (needsRecovery ? 1.3 : 1));
        }
        return best;
    }
}
