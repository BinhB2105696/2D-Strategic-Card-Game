using UnityEngine;

[CreateAssetMenu(
    fileName = "Resolution_Increase_Defense_Until_End_Of_Turn",
    menuName = "Card Game/Effects/Resolutions/Increase Defense Until End Of Turn")]
public sealed class IncreaseDefenseUntilEndOfTurnResolutionAsset :
    EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(
            EffectOperationType.IncreaseDefenseUntilEndOfTurn,
            amount);
    }
}
