using UnityEngine;

[CreateAssetMenu(
    fileName = "Protect_Attacked_Monster",
    menuName = "Card Game/Effects/Resolution/Protect Attacked Monster")]
public sealed class ProtectAttackedMonsterResolutionAsset : EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(
            EffectOperationType.ProtectAttackedMonster,
            amount);
    }
}
