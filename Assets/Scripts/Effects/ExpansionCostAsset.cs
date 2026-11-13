using UnityEngine;

[CreateAssetMenu(fileName = "Expansion_Cost", menuName = "Card Game/Effects/Cost/Expansion Cost")]
public sealed class ExpansionCostAsset : EffectCostAsset
{
    [SerializeField] private EffectCostType cost = EffectCostType.AvailableEnergy;
    [SerializeField] private int secondaryAmount = 0;
    public override EffectCostDefinition CreateDefinition(int amount) => new EffectCostDefinition(cost, amount, secondaryAmount);
}
