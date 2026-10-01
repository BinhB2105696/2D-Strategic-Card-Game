using System;
using System.Collections.Generic;

public static class EffectResolver
{
    public static ActionResult TryResolve(
        EffectContext context,
        IEffectCondition condition,
        IEffectCost cost,
        IEffectTargetRule targetRule,
        ICardEffect effect)
    {
        if (context == null)
        {
            return ActionResult.Failure("EffectContext is required.");
        }

        if (condition == null)
        {
            return ActionResult.Failure("Effect condition is required.");
        }

        if (cost == null)
        {
            return ActionResult.Failure("Effect cost is required.");
        }

        if (targetRule == null)
        {
            return ActionResult.Failure("Effect target rule is required.");
        }

        if (effect == null)
        {
            return ActionResult.Failure("Card effect is required.");
        }

        string failureReason;

        if (!condition.IsMet(context, out failureReason))
        {
            return ActionResult.Failure(NormalizeReason(
                failureReason,
                "The effect condition is not met."));
        }

        if (!cost.CanPay(context, out failureReason))
        {
            return ActionResult.Failure(NormalizeReason(
                failureReason,
                "The effect cost cannot be paid."));
        }

        // Targets are prevalidated before any state mutation. This keeps an
        // invalid network/replay command from paying a cost and leaving a
        // partially changed GameState.
        if (!targetRule.AreTargetsValid(context, out failureReason))
        {
            return ActionResult.Failure(NormalizeReason(
                failureReason,
                "The selected effect targets are invalid."));
        }

        List<GameEvent> events = new List<GameEvent>();
        cost.Pay(context, events);
        effect.Resolve(context, events);
        return ActionResult.Success(events);
    }

    private static string NormalizeReason(
        string failureReason,
        string fallback)
    {
        return string.IsNullOrWhiteSpace(failureReason)
            ? fallback
            : failureReason;
    }
}
