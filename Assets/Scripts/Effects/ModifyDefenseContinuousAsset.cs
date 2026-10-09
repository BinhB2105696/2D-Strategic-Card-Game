using UnityEngine;

[CreateAssetMenu(
    fileName = "Continuous_Modify_Defense",
    menuName = "Card Game/Effects/Continuous/Modify Defense")]
public sealed class ModifyDefenseContinuousAsset : ContinuousModifierAsset
{
    public override IContinuousModifier CreateModifier(int amount)
    {
        return new DefenseContinuousModifier(amount);
    }
}

public sealed class DefenseContinuousModifier : IContinuousModifier
{
    private readonly int amount;

    public DefenseContinuousModifier(int amount)
    {
        this.amount = amount;
    }

    public int ModifyHealth(CardInstance source, CardInstance target) => 0;
    public int ModifyAttack(CardInstance source, CardInstance target) => 0;
    public int ModifyDefense(CardInstance source, CardInstance target) => amount;
}
