using System.Collections.Generic;

public interface IEffectCondition
{
    bool IsMet(EffectContext context, out string failureReason);
}

public interface IEffectCost
{
    bool CanPay(EffectContext context, out string failureReason);

    void Pay(EffectContext context, List<GameEvent> events);
}

public interface IEffectTargetRule
{
    bool AreTargetsValid(
        EffectContext context,
        out string failureReason);
}

public interface ICardEffect
{
    void Resolve(EffectContext context, List<GameEvent> events);
}
