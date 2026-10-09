using UnityEngine;

[CreateAssetMenu(
    fileName = "Continuous_Modify_Attack",
    menuName = "Card Game/Effects/Continuous/Modify Attack")]
public sealed class ModifyAttackContinuousAsset : ContinuousModifierAsset
{
    public override IContinuousModifier CreateModifier(int amount)
    {
        return new AttackContinuousModifier(amount);
    }
}

public sealed class AttackContinuousModifier : IContinuousModifier
{
    private readonly int amount;

    public AttackContinuousModifier(int amount)
    {
        this.amount = amount;
    }

    public int ModifyHealth(CardInstance source, CardInstance target) => 0;
    public int ModifyAttack(CardInstance source, CardInstance target) => amount;
    public int ModifyDefense(CardInstance source, CardInstance target) => 0;
}
