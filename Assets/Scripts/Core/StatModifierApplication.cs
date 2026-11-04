using System;

[Serializable]
public sealed class StatModifierApplication
{
    public long SourceInstanceId { get; }
    public CardInstance Target { get; }
    public int AttackAmount { get; }
    public int DefenseAmount { get; }
    public int MaximumHealthAmount { get; }
    public int ExpireAfterTurn { get; }
    public bool IsTemporary => ExpireAfterTurn > 0;

    public StatModifierApplication(
        long sourceInstanceId,
        CardInstance target,
        int attackAmount,
        int defenseAmount,
        int expireAfterTurn,
        int maximumHealthAmount = 0)
    {
        SourceInstanceId = sourceInstanceId;
        Target = target ?? throw new ArgumentNullException(nameof(target));
        AttackAmount = attackAmount;
        DefenseAmount = defenseAmount;
        ExpireAfterTurn = expireAfterTurn;
        MaximumHealthAmount = maximumHealthAmount;
    }

    public void Apply()
    {
        Target.ModifyAttack(AttackAmount);
        Target.ModifyDefense(DefenseAmount);
        Target.ModifyMaximumHealth(MaximumHealthAmount);
    }

    public void Remove()
    {
        Target.ModifyAttack(-AttackAmount);
        Target.ModifyDefense(-DefenseAmount);
        Target.ModifyMaximumHealth(-MaximumHealthAmount);
    }
}
