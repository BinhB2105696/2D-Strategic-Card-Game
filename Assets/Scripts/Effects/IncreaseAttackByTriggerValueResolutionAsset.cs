using UnityEngine;

[CreateAssetMenu(
    fileName = "Increase_Attack_By_Trigger_Value",
    menuName = "Card Game/Effects/Resolution/Increase Attack By Trigger Value")]
public sealed class IncreaseAttackByTriggerValueResolutionAsset : EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(
            EffectOperationType.IncreaseAttackByTriggerValue,
            amount);
    }
}
