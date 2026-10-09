using System;

[Serializable]
public sealed class StatModifierApplication
{
    public long SourceInstanceId { get; }
    public CardInstance Target { get; }
    public int AttackAmount { get; }
    public int DefenseAmount { get; }
    public int ExpireAfterTurn { get; }
    public bool IsTemporary => ExpireAfterTurn > 0;

    public StatModifierApplication(
        long sourceInstanceId,
        CardInstance target,
        int attackAmount,
        int defenseAmount,
        int expireAfterTurn)
    {
        SourceInstanceId = sourceInstanceId;
        Target = target ?? throw new ArgumentNullException(nameof(target));
        AttackAmount = attackAmount;
        DefenseAmount = defenseAmount;
        ExpireAfterTurn = expireAfterTurn;
    }

    public void Apply()
    {
        Target.ModifyAttack(AttackAmount);
        Target.ModifyDefense(DefenseAmount);
    }

    public void Remove()
    {
        Target.ModifyAttack(-AttackAmount);
        Target.ModifyDefense(-DefenseAmount);
    }
}
