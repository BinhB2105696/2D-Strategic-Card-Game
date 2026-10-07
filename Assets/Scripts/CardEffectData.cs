using System;
using System.Collections.Generic;
using UnityEngine;

public enum EffectKind { Activated, Continuous }

public enum EffectTurnScope { YourTurn, OpponentTurn, EitherTurn }

public enum EffectActivationWindow { OpenGameState, ResponseToActivation }

public enum EffectUsageScope { PerInstance, PerCardId }

public enum EffectUsageReset { PerTurn, PerMatch }

[Flags]
public enum EffectPhaseMask
{
    None = 0,
    Recovery = 1 << 0,
    Standby = 1 << 1,
    Main = 1 << 2,
    Battle = 1 << 3,
    End = 1 << 4,
    Any = Recovery | Standby | Main | Battle | End
}

[Serializable]
public sealed class EffectConditionSlot
{
    [SerializeField] private EffectConditionAsset component;
    [Tooltip("Only used by conditions that require a number.")]
    [Min(0)] [SerializeField] private int amount;
    public EffectConditionAsset Component => component;
    public int Amount => amount;
}

[Serializable]
public sealed class EffectCostSlot
{
    [SerializeField] private EffectCostAsset component;
    [Tooltip("Quantity to pay; for Pay Energy, this is the Energy cost.")]
    [Min(1)] [SerializeField] private int amount = 1;
    public EffectCostAsset Component => component;
    public int Amount => amount;
}

[Serializable]
public sealed class EffectTargetSlot
{
    [SerializeField] private EffectTargetAsset component;
    [Tooltip("Exact number of targets to select for this component.")]
    [Min(1)] [SerializeField] private int amount = 1;
    public EffectTargetAsset Component => component;
    public int Amount => amount;
}

[Serializable]
public sealed class EffectResolutionSlot
{
    [SerializeField] private EffectResolutionAsset component;
    [Tooltip("Quantity produced; for Draw Cards, this is the number of cards drawn.")]
    [Min(1)] [SerializeField] private int amount = 1;
    public EffectResolutionAsset Component => component;
    public int Amount => amount;
}

[Serializable]
public sealed class ContinuousModifierSlot
{
    [SerializeField] private ContinuousModifierAsset component;
    [SerializeField] private int amount;
    public ContinuousModifierAsset Component => component;
    public int Amount => amount;
}

[Serializable]
public sealed class CardEffectData
{
    [SerializeField] private string effectId;
    [SerializeField] private EffectKind effectKind = EffectKind.Activated;
    [SerializeField] private EffectTriggerType trigger =
        EffectTriggerType.ManualActivation;
    [SerializeField] private EffectPhaseMask allowedPhases = EffectPhaseMask.Main;
    [SerializeField] private EffectTurnScope allowedTurn = EffectTurnScope.YourTurn;
    [SerializeField] private EffectActivationWindow activationWindow =
        EffectActivationWindow.OpenGameState;
    [SerializeField] private List<EffectConditionSlot> conditions =
        new List<EffectConditionSlot>();
    [SerializeField] private List<EffectCostSlot> costs =
        new List<EffectCostSlot>();
    [SerializeField] private List<EffectTargetSlot> targets =
        new List<EffectTargetSlot>();
    [SerializeField] private List<EffectResolutionSlot> resolution =
        new List<EffectResolutionSlot>();
    [SerializeField] private List<ContinuousModifierSlot> continuousModifiers =
        new List<ContinuousModifierSlot>();
    [Min(0)] [SerializeField] private int maxUses;
    [SerializeField] private EffectUsageScope countBy = EffectUsageScope.PerInstance;
    [SerializeField] private EffectUsageReset reset = EffectUsageReset.PerTurn;

    public string EffectId => effectId;
    public EffectKind Kind => effectKind;
    public EffectTriggerType Trigger => trigger;
    public EffectPhaseMask AllowedPhases => allowedPhases;
    public EffectTurnScope AllowedTurn => allowedTurn;
    public EffectActivationWindow ActivationWindow => activationWindow;
    public IReadOnlyList<EffectConditionSlot> Conditions => conditions;
    public IReadOnlyList<EffectCostSlot> Costs => costs;
    public IReadOnlyList<EffectTargetSlot> Targets => targets;
    public IReadOnlyList<EffectResolutionSlot> Resolution => resolution;
    public IReadOnlyList<ContinuousModifierSlot> ContinuousModifiers =>
        continuousModifiers;
    public int MaxUses => maxUses;
    public EffectUsageScope CountBy => countBy;
    public EffectUsageReset Reset => reset;
}
