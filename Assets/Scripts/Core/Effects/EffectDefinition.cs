using System;
using System.Collections.Generic;

public enum EffectTriggerType
{
    ManualActivation,
    OnSummoned,
    OnDestroyed,
    StandbyPhase
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
    public TurnPhase ActivationPhase { get; }
    public IReadOnlyList<EffectConditionDefinition> Conditions { get; }
    public IReadOnlyList<EffectCostDefinition> Costs { get; }
    public EffectTargetType TargetType { get; }
    public IReadOnlyList<EffectOperationDefinition> Operations { get; }
    public int MaxUsesPerTurn { get; }

    public EffectDefinition(
        string effectId,
        EffectTriggerType trigger,
        TurnPhase activationPhase,
        IReadOnlyList<EffectConditionDefinition> conditions,
        IReadOnlyList<EffectCostDefinition> costs,
        EffectTargetType targetType,
        IReadOnlyList<EffectOperationDefinition> operations,
        int maxUsesPerTurn = 0)
    {
        if (string.IsNullOrWhiteSpace(effectId))
        {
            throw new ArgumentException(
                "An effect definition requires a stable Effect ID.",
                nameof(effectId));
        }

        EffectId = effectId;
        Trigger = trigger;
        ActivationPhase = activationPhase;
        Conditions = CopyList(conditions, NoConditions, nameof(conditions));
        Costs = CopyList(costs, NoCosts, nameof(costs));
        TargetType = targetType;
        Operations = CopyList(operations, NoOperations, nameof(operations));
        MaxUsesPerTurn = Math.Max(0, maxUsesPerTurn);
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
