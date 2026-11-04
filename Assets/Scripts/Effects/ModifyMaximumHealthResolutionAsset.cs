using UnityEngine;

// Amount is authored in the effect slot. Both directions preserve damage.
[CreateAssetMenu(menuName = "Card Game/Effects/Resolution/Modify Maximum Health")]
public sealed class ModifyMaximumHealthResolutionAsset : EffectResolutionAsset
{
    [SerializeField] private bool decrease;

    public override EffectOperationDefinition CreateDefinition(int amount) =>
        new EffectOperationDefinition(decrease ? EffectOperationType.ReduceMaximumHealth :
            EffectOperationType.IncreaseMaximumHealth, amount);
}
