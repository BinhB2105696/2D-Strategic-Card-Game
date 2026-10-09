using UnityEngine;

[CreateAssetMenu(
    fileName = "Negate_Previous_Activation",
    menuName = "Card Game/Effects/Resolution/Negate Previous Activation")]
public sealed class NegatePreviousActivationResolutionAsset :
    EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(
            EffectOperationType.NegatePreviousChainLink,
            1);
    }
}
