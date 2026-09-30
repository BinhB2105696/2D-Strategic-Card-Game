using System;

public enum CardZone
{
    Deck,
    Hand,
    MonsterZone,
    SpellTrapZone,
    Graveyard
}

public enum BattlePosition
{
    None,
    Attack,
    Defense
}

[Serializable]
public sealed class CardInstance
{
    public long InstanceId { get; }
    public int OwnerPlayerIndex { get; }
    public CardDefinition Definition { get; }

    public int CurrentHealth { get; private set; }
    public int CurrentAttack { get; private set; }
    public int CurrentDefense { get; private set; }
    public int AllocatedEnergy { get; private set; }

    public CardZone Zone { get; private set; }
    public BattlePosition BattlePosition { get; private set; }
    public bool IsFaceDown { get; private set; }
    public bool WasSummonedThisTurn { get; private set; }
    public bool HasAttackedThisTurn { get; private set; }
    public bool HasChangedPositionThisTurn { get; private set; }

    public bool IsDestroyed =>
        Definition.IsMonster && CurrentHealth <= 0;

    public CardInstance(
        long instanceId,
        int ownerPlayerIndex,
        CardDefinition definition)
    {
        if (instanceId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(instanceId),
                "Instance ID must be greater than zero.");
        }

        if (ownerPlayerIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ownerPlayerIndex),
                "Owner player index cannot be negative.");
        }

        Definition = definition ??
            throw new ArgumentNullException(nameof(definition));

        InstanceId = instanceId;
        OwnerPlayerIndex = ownerPlayerIndex;
        CurrentHealth = definition.IsMonster ? definition.BaseHealth : 0;
        CurrentAttack = definition.IsMonster ? definition.BaseAttack : 0;
        CurrentDefense = definition.IsMonster ? definition.BaseDefense : 0;
        Zone = CardZone.Deck;
        BattlePosition = BattlePosition.None;
        IsFaceDown = true;
    }

    public void MoveToZone(CardZone destination, bool faceDown)
    {
        Zone = destination;
        IsFaceDown = faceDown;

        if (destination != CardZone.MonsterZone)
        {
            BattlePosition = BattlePosition.None;
        }
    }

    public void Summon(BattlePosition position)
    {
        if (!Definition.IsMonster)
        {
            throw new InvalidOperationException(
                "Only a Monster card can be summoned.");
        }

        if (position == BattlePosition.None)
        {
            throw new ArgumentException(
                "A summoned Monster requires Attack or Defense position.",
                nameof(position));
        }

        Zone = CardZone.MonsterZone;
        BattlePosition = position;
        IsFaceDown = false;
        WasSummonedThisTurn = true;
    }

    public void AllocateEnergy(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        AllocatedEnergy += amount;
    }

    public int RemoveAllocatedEnergy(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        int removedAmount = Math.Min(amount, AllocatedEnergy);
        AllocatedEnergy -= removedAmount;
        return removedAmount;
    }

    public void ReceiveDamage(int amount)
    {
        if (!Definition.IsMonster)
        {
            throw new InvalidOperationException(
                "Only a Monster card can receive monster damage.");
        }

        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        CurrentHealth = Math.Max(0, CurrentHealth - amount);
    }

    public void ModifyAttack(int amount)
    {
        CurrentAttack = Math.Max(0, CurrentAttack + amount);
    }

    public void ModifyDefense(int amount)
    {
        CurrentDefense = Math.Max(0, CurrentDefense + amount);
    }

    public void SetBattlePosition(BattlePosition position)
    {
        if (!Definition.IsMonster || Zone != CardZone.MonsterZone)
        {
            throw new InvalidOperationException(
                "Only a Monster in a Monster Zone has a battle position.");
        }

        if (position == BattlePosition.None)
        {
            throw new ArgumentException(
                "Use Attack or Defense position.",
                nameof(position));
        }

        BattlePosition = position;
        HasChangedPositionThisTurn = true;
    }

    public void MarkAttacked()
    {
        HasAttackedThisTurn = true;
    }

    public void ResetTurnFlags()
    {
        WasSummonedThisTurn = false;
        HasAttackedThisTurn = false;
        HasChangedPositionThisTurn = false;
    }
}
