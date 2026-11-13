using UnityEngine;

[CreateAssetMenu(fileName = "After_First_Turn", menuName = "Card Game/Effects/Condition/After First Turn")]
public sealed class AfterFirstTurnConditionAsset : EffectConditionAsset
{
    public override EffectConditionDefinition CreateDefinition(int amount)
        => new EffectConditionDefinition(EffectConditionType.AfterFirstTurn, 0);
}
