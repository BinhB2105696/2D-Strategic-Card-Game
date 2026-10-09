using UnityEngine;

[CreateAssetMenu(
    fileName = "Reduce_Attack_Until_End_Of_Turn",
    menuName = "Card Game/Effects/Resolution/Reduce Attack Until End Of Turn")]
public sealed class ReduceAttackUntilEndOfTurnResolutionAsset :
    EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(
            EffectOperationType.ReduceAttackUntilEndOfTurn,
            amount);
    }
}
