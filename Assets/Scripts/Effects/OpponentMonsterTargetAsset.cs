using UnityEngine;

[CreateAssetMenu(
    fileName = "Target_Opponent_Monster",
    menuName = "Card Game/Effects/Targets/Opponent Monster")]
public sealed class OpponentMonsterTargetAsset : EffectTargetAsset
{
    public override EffectTargetType CreateDefinition()
    {
        return EffectTargetType.OpponentMonster;
    }
}
