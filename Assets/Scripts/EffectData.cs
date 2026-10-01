using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EffectConditionData
{
    [SerializeField] private EffectConditionType type;
    [Min(0)] [SerializeField] private int amount;

    public EffectConditionType Type => type;
    public int Amount => amount;

    public void ClampValues()
    {
        amount = Mathf.Max(0, amount);
    }
}

[Serializable]
public sealed class EffectCostData
{
    [SerializeField] private EffectCostType type;
    [Min(0)] [SerializeField] private int amount;

    public EffectCostType Type => type;
    public int Amount => amount;

    public void ClampValues()
    {
        amount = Mathf.Max(0, amount);
    }
}

[Serializable]
public sealed class EffectOperationData
{
    [SerializeField] private EffectOperationType type;
    [Min(0)] [SerializeField] private int amount = 1;

    public EffectOperationType Type => type;
    public int Amount => amount;

    public void ClampValues()
    {
        amount = Mathf.Max(0, amount);
    }
}

[CreateAssetMenu(fileName = "New Effect", menuName = "Card Game/Effect Data")]
public sealed class EffectData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string effectId;

    [Header("Activation")]
    [SerializeField] private EffectTriggerType trigger;
    [SerializeField] private TurnPhase activationPhase = TurnPhase.Main;
    [Min(0)] [SerializeField] private int maxUsesPerTurn;

    [Header("Requirements")]
    [SerializeField] private List<EffectConditionData> conditions =
        new List<EffectConditionData>();
    [SerializeField] private List<EffectCostData> costs =
        new List<EffectCostData>();

    [Header("Target and Resolution")]
    [SerializeField] private EffectTargetType targetType;
    [SerializeField] private List<EffectOperationData> operations =
        new List<EffectOperationData>();

    public string EffectId => effectId;
    public EffectTriggerType Trigger => trigger;
    public TurnPhase ActivationPhase => activationPhase;
    public int MaxUsesPerTurn => maxUsesPerTurn;
    public IReadOnlyList<EffectConditionData> Conditions => conditions;
    public IReadOnlyList<EffectCostData> Costs => costs;
    public EffectTargetType TargetType => targetType;
    public IReadOnlyList<EffectOperationData> Operations => operations;

    private void OnValidate()
    {
        maxUsesPerTurn = Mathf.Max(0, maxUsesPerTurn);
        ClampList(conditions, item => item.ClampValues());
        ClampList(costs, item => item.ClampValues());
        ClampList(operations, item => item.ClampValues());
    }

    private static void ClampList<T>(List<T> values, Action<T> clamp)
        where T : class
    {
        if (values == null)
        {
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            if (values[index] != null)
            {
                clamp(values[index]);
            }
        }
    }
}
