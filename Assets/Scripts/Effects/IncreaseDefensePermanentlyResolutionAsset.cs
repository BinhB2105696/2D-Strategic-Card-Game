using UnityEngine;

[CreateAssetMenu(
    fileName = "Increase_Defense_Permanently",
    menuName = "Card Game/Effects/Resolution/Increase Defense Permanently")]
public sealed class IncreaseDefensePermanentlyResolutionAsset : EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(
            EffectOperationType.IncreaseDefensePermanently,
            amount);
    }
}
