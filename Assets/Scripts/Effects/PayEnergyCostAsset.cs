using UnityEngine;

[CreateAssetMenu(fileName = "Pay Energy", menuName = "Card Game/Effect Components/Costs/Pay Energy")]
public sealed class PayEnergyCostAsset : EffectCostAsset
{
    public override EffectCostDefinition CreateDefinition(int amount)
    {
        return new EffectCostDefinition(EffectCostType.AvailableEnergy, amount);
    }
}
