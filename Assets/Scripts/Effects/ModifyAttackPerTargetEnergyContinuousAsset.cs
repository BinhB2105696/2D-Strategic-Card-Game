using UnityEngine;

[CreateAssetMenu(
    fileName = "Modify_Attack_Per_Target_Energy",
    menuName = "Card Game/Effects/Continuous/Modify Attack Per Target Energy")]
public sealed class ModifyAttackPerTargetEnergyContinuousAsset : ContinuousModifierAsset
{
    public override IContinuousModifier CreateModifier(int amount)
    {
        return new AttackPerTargetEnergyContinuousModifier(amount);
    }
}

public sealed class AttackPerTargetEnergyContinuousModifier : IContinuousModifier
{
    private readonly int amountPerEnergy;

    public AttackPerTargetEnergyContinuousModifier(int amountPerEnergy)
    {
        this.amountPerEnergy = amountPerEnergy;
    }

    public int ModifyAttack(CardInstance source, CardInstance target)
    {
        return amountPerEnergy * target.AllocatedEnergy;
    }

    public int ModifyDefense(CardInstance source, CardInstance target)
    {
        return 0;
    }

    public int ModifyHealth(CardInstance source, CardInstance target)
    {
        return 0;
    }
}
