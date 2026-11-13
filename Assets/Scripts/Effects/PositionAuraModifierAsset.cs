using UnityEngine;

[CreateAssetMenu(fileName = "Position_Aura", menuName = "Card Game/Effects/Continuous/Position Aura")]
public sealed class PositionAuraModifierAsset : ContinuousModifierAsset
{
    [SerializeField] private bool defense = false;
    public override IContinuousModifier CreateModifier(int amount) => new PositionAuraModifier(defense, amount);
}
