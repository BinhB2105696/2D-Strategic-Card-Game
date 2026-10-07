using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Skip Next Opponent Draw",
    menuName = "Card Game/Effect Components/Resolutions/Skip Next Opponent Draw")]
public sealed class SkipNextOpponentDrawResolutionAsset : EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        if (amount != 1)
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Skip Next Opponent Draw requires Amount 1.");

        return new EffectOperationDefinition(
            EffectOperationType.SkipNextOpponentDrawStep,
            amount);
    }
}
