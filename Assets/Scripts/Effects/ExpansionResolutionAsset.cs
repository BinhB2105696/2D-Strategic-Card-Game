using UnityEngine;

[CreateAssetMenu(fileName = "Expansion_Resolution", menuName = "Card Game/Effects/Resolution/Expansion Resolution")]
public sealed class ExpansionResolutionAsset : EffectResolutionAsset
{
    [SerializeField] private EffectOperationType operation = EffectOperationType.None;
    [SerializeField] private int secondaryAmount = 0;
    public override EffectOperationDefinition CreateDefinition(int amount) => new EffectOperationDefinition(operation, amount, secondaryAmount);
}
