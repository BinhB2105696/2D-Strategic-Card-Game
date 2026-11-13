using System;

public enum CardZone
{
    Deck,
    Hand,
    MonsterZone,
    SpellTrapZone,
    Graveyard,
    FieldZone
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
    public const int DefaultMaxAttacksPerTurn = 1;

    public long InstanceId { get; }
    public int OwnerPlayerIndex { get; }
    public CardDefinition Definition { get; }

    // Keep damage independent of the maximum so adding/removing a modifier
    // never heals damage, even when a reduction temporarily takes HP below zero.
    private int damageTaken;
    private int maximumHealthAdjustment;
    public int MaximumHealth => Definition.IsMonster
        ? Math.Max(0, checked(Definition.BaseHealth + maximumHealthAdjustment)) : 0;
    public int CurrentHealth => Math.Max(0, MaximumHealth - damageTaken);
    public int CurrentAttack => Math.Max(
        0,
        Definition.BaseAttack + attackAdjustment);
    public int CurrentDefense => Math.Max(
        0,
        Definition.BaseDefense + defenseAdjustment);
    public int AllocatedEnergy { get; private set; }
    public int AttackPriority => CardExpansion.PassiveAmount(this, EffectOperationType.AttackPriority);

    public CardZone Zone { get; private set; }
    public BattlePosition BattlePosition { get; private set; }
    public bool IsFaceDown { get; private set; }
    public bool WasSummonedThisTurn { get; private set; }
    public int AttacksUsedThisTurn { get; private set; }
    public int MaxAttacksThisTurn { get; private set; }
    public bool HasChangedPositionThisTurn { get; private set; }
    public int SetTurnNumber { get; private set; }
    public long EquippedTargetInstanceId { get; private set; }

    private int attackAdjustment;
    private int defenseAdjustment;

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
        Zone = CardZone.Deck;
        BattlePosition = BattlePosition.None;
        IsFaceDown = true;
        MaxAttacksThisTurn = definition.IsMonster
            ? DefaultMaxAttacksPerTurn
            : 0;
        SetTurnNumber = -1;
    }

    public void MoveToZone(CardZone destination, bool faceDown)
    {
        Zone = destination;
        IsFaceDown = faceDown;

        if (destination != CardZone.MonsterZone)
        {
            BattlePosition = BattlePosition.None;
        }

        if (destination != CardZone.SpellTrapZone)
        {
            SetTurnNumber = -1;
            EquippedTargetInstanceId = 0;
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

    public void ClearAllocatedEnergy()
    {
        AllocatedEnergy = 0;
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

        damageTaken = checked(damageTaken + Math.Min(amount, CurrentHealth));
    }

    public int RestoreHealth(int amount)
    {
        if (!Definition.IsMonster)
            throw new InvalidOperationException(
                "Only a Monster can restore Health.");
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        if (IsDestroyed) return 0; // Healing is not revival.
        int restored = Math.Min(amount, damageTaken);
        damageTaken -= restored;
        return restored;
    }

    public void ModifyMaximumHealth(int amount)
    {
        if (!Definition.IsMonster)
            throw new InvalidOperationException("Only a Monster has maximum HP.");
        maximumHealthAdjustment = checked(maximumHealthAdjustment + amount);
    }

    public void ResetHealthToBase()
    {
        if (Definition.IsMonster)
        {
            maximumHealthAdjustment = 0;
            damageTaken = 0;
        }
    }

    public void ModifyAttack(int amount)
    {
        attackAdjustment += amount;
    }

    public void ModifyDefense(int amount)
    {
        defenseAdjustment += amount;
    }

    public void SetFaceDownInSpellTrapZone(int turnNumber)
    {
        if (Zone != CardZone.SpellTrapZone)
            throw new InvalidOperationException(
                "Only a card in a Spell/Trap Zone can be Set.");

        IsFaceDown = true;
        SetTurnNumber = turnNumber;
    }

    public void Reveal()
    {
        IsFaceDown = false;
    }

    public void EquipTo(long targetInstanceId)
    {
        if (targetInstanceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetInstanceId));

        EquippedTargetInstanceId = targetInstanceId;
        IsFaceDown = false;
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
        AttacksUsedThisTurn++;
    }

    public void ModifyMaxAttacksThisTurn(int amount)
    {
        MaxAttacksThisTurn = Math.Max(0, MaxAttacksThisTurn + amount);
    }

    public void ResetTurnFlags()
    {
        WasSummonedThisTurn = false;
        AttacksUsedThisTurn = 0;
        MaxAttacksThisTurn = Definition.IsMonster
            ? DefaultMaxAttacksPerTurn
            : 0;
        HasChangedPositionThisTurn = false;
    }
}
