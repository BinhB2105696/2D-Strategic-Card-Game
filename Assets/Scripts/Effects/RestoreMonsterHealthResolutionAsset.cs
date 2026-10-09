using UnityEngine;

[CreateAssetMenu(
    fileName = "Resolution_Restore_Monster_Health",
    menuName = "Card Game/Effects/Resolutions/Restore Monster Health")]
public sealed class RestoreMonsterHealthResolutionAsset : EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(
            EffectOperationType.RestoreMonsterHealth,
            amount);
    }
}
