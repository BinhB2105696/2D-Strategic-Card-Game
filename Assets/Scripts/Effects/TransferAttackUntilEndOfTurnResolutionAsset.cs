using UnityEngine;

[CreateAssetMenu(
    fileName = "Transfer_Attack_Until_End_Of_Turn",
    menuName = "Card Game/Effects/Resolution/Transfer Attack Until End Of Turn")]
public sealed class TransferAttackUntilEndOfTurnResolutionAsset : EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(
            EffectOperationType.TransferAttackUntilEndOfTurn,
            amount);
    }
}
