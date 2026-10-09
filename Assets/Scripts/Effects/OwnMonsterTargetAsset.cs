using UnityEngine;

[CreateAssetMenu(
    fileName = "Target_Own_Monster",
    menuName = "Card Game/Effects/Targets/Own Monster")]
public sealed class OwnMonsterTargetAsset : EffectTargetAsset
{
    public override EffectTargetType CreateDefinition()
    {
        return EffectTargetType.OwnMonster;
    }
}
