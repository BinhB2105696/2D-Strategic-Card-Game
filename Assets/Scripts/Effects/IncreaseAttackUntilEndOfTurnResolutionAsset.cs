using UnityEngine;

[CreateAssetMenu(
    fileName = "Resolution_Increase_Attack_Until_End_Of_Turn",
    menuName = "Card Game/Effects/Resolutions/Increase Attack Until End Of Turn")]
public sealed class IncreaseAttackUntilEndOfTurnResolutionAsset :
    EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(
            EffectOperationType.IncreaseAttackUntilEndOfTurn,
            amount);
    }
}
