using UnityEngine;

[CreateAssetMenu(fileName = "Draw Cards", menuName = "Card Game/Effect Components/Resolutions/Draw Cards")]
public sealed class DrawCardsResolutionAsset : EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(EffectOperationType.DrawCards, amount);
    }
}
