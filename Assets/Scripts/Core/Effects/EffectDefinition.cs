using System;
using System.Collections.Generic;

public enum EffectTriggerType
{
    ManualActivation,
    OnSummoned,
    OnDestroyed,
    StandbyPhase,
    OnSpellTrapActivated
}

public enum EffectConditionType
{
    None
}

public enum EffectCostType
{
    AvailableEnergy,
    PlayerHealth,
    DiscardFromHand,
    TributeMonster,
    RemoveAllocatedEnergy
}

public enum EffectTargetType
{
    None,
    OwnMonster,
    OpponentMonster
}

public enum EffectOperationType
{
    None,
    DrawCards
}

[Serializable]
public sealed class EffectConditionDefinition
{
    public EffectConditionType Type { get; }
    public int Amount { get; }

    public EffectConditionDefinition(
        EffectConditionType type,
        int amount)
    {
        Type = type;
        Amount = Math.Max(0, amount);
    }
}

[Serializable]
public sealed class EffectCostDefinition
{
    public EffectCostType Type { get; }
    public int Amount { get; }

    public EffectCostDefinition(EffectCostType type, int amount)
    {
        Type = type;
        Amount = Math.Max(0, amount);
    }
}

[Serializable]
public sealed class EffectOperationDefinition
{
    public EffectOperationType Type { get; }
    public int Amount { get; }

    public EffectOperationDefinition(
        EffectOperationType type,
        int amount)
    {
        Type = type;
        Amount = Math.Max(0, amount);
    }
}

[Serializable]
public sealed class EffectDefinition
{
    private static readonly EffectConditionDefinition[] NoConditions =
        new EffectConditionDefinition[0];
    private static readonly EffectCostDefinition[] NoCosts =
        new EffectCostDefinition[0];
    private static readonly EffectOperationDefinition[] NoOperations =
        new EffectOperationDefinition[0];

    public string EffectId { get; }
    public EffectTriggerType Trigger { get; }
    public EffectKind Kind { get; }
    public EffectPhaseMask AllowedPhases { get; }
    public EffectTurnScope AllowedTurn { get; }
    public EffectActivationWindow ActivationWindow { get; }
    public IReadOnlyList<EffectConditionDefinition> Conditions { get; }
    public IReadOnlyList<EffectCostDefinition> Costs { get; }
    public EffectTargetType TargetType { get; }
    public int TargetCount { get; }
    public IReadOnlyList<EffectOperationDefinition> Operations { get; }
    public int MaxUses { get; }
    public EffectUsageScope CountBy { get; }
    public EffectUsageReset Reset { get; }
    public IReadOnlyList<IContinuousModifier> ContinuousModifiers { get; }

    public EffectDefinition(
        string effectId,
        EffectTriggerType trigger,
        EffectKind kind,
        EffectPhaseMask allowedPhases,
        EffectTurnScope allowedTurn,
        EffectActivationWindow activationWindow,
        IReadOnlyList<EffectConditionDefinition> conditions,
        IReadOnlyList<EffectCostDefinition> costs,
        EffectTargetType targetType,
        int targetCount,
        IReadOnlyList<EffectOperationDefinition> operations,
        int maxUses = 0,
        EffectUsageScope countBy = EffectUsageScope.PerInstance,
        EffectUsageReset reset = EffectUsageReset.PerTurn,
        IReadOnlyList<IContinuousModifier> continuousModifiers = null)
    {
        if (string.IsNullOrWhiteSpace(effectId))
        {
            throw new ArgumentException(
                "An effect definition requires a stable Effect ID.",
                nameof(effectId));
        }

        EffectId = effectId;
        Trigger = trigger;
        Kind = kind;
        AllowedPhases = allowedPhases;
        AllowedTurn = allowedTurn;
        ActivationWindow = activationWindow;
        Conditions = CopyList(conditions, NoConditions, nameof(conditions));
        Costs = CopyList(costs, NoCosts, nameof(costs));
        TargetType = targetType;
        TargetCount = Math.Max(0, targetCount);
        Operations = CopyList(operations, NoOperations, nameof(operations));
        MaxUses = Math.Max(0, maxUses);
        CountBy = countBy;
        Reset = reset;
        ContinuousModifiers = CopyList(
            continuousModifiers,
            new IContinuousModifier[0],
            nameof(continuousModifiers));
    }

    private static IReadOnlyList<T> CopyList<T>(
        IReadOnlyList<T> source,
        T[] empty,
        string parameterName)
        where T : class
    {
        if (source == null || source.Count == 0)
        {
            return empty;
        }

        T[] copy = new T[source.Count];

        for (int index = 0; index < source.Count; index++)
        {
            copy[index] = source[index] ?? throw new ArgumentException(
                $"Definition at index {index} is null.",
                parameterName);
        }

        return copy;
    }
}
