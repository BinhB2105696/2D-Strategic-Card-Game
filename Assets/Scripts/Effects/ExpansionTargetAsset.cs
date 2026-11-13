using UnityEngine;

[CreateAssetMenu(fileName = "Expansion_Target", menuName = "Card Game/Effects/Target/Expansion Target")]
public sealed class ExpansionTargetAsset : EffectTargetAsset
{
    [SerializeField] private EffectTargetType target = EffectTargetType.None;
    public override EffectTargetType CreateDefinition() => target;
}
