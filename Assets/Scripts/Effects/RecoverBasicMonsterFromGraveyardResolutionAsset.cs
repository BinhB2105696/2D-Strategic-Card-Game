using UnityEngine;

[CreateAssetMenu(
    fileName = "Recover_Basic_Monster_From_Graveyard",
    menuName = "Card Game/Effects/Resolution/Recover Basic Monster From Graveyard")]
public sealed class RecoverBasicMonsterFromGraveyardResolutionAsset : EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(
            EffectOperationType.RecoverBasicMonsterFromGraveyard,
            amount);
    }
}
