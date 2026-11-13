using UnityEngine;

[CreateAssetMenu(fileName = "Source_In_Defense", menuName = "Card Game/Effects/Condition/Source In Defense")]
public sealed class SourceInDefenseConditionAsset : EffectConditionAsset
{
    public override EffectConditionDefinition CreateDefinition(int amount)
        => new EffectConditionDefinition(EffectConditionType.SourceInDefense, 0);
}
