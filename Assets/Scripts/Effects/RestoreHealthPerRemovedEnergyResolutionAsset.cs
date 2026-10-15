using UnityEngine;

[CreateAssetMenu(
    fileName = "Restore_Health_Per_Removed_Energy",
    menuName = "Card Game/Effects/Resolution/Restore Health Per Removed Energy")]
public sealed class RestoreHealthPerRemovedEnergyResolutionAsset : EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(
            EffectOperationType.RestoreHealthPerRemovedAllocatedEnergy,
            amount);
    }
}
