using UnityEngine;

[CreateAssetMenu(
    fileName = "Skip_Current_Battle_Phase",
    menuName = "Card Game/Effects/Resolution/Skip Current Battle Phase")]
public sealed class SkipCurrentBattlePhaseResolutionAsset : EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(
            EffectOperationType.SkipCurrentBattlePhase,
            amount);
    }
}
