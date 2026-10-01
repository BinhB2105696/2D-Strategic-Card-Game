using System;
using System.Collections.Generic;

[Serializable]
public sealed class PlayerState
{
    private readonly List<CardInstance> deck;
    private readonly List<CardInstance> hand;
    private readonly List<CardInstance> graveyard;
    private readonly CardInstance[] monsterZones;
    private readonly CardInstance[] spellTrapZones;

    public int PlayerIndex { get; }
    public int Health { get; private set; }
    public int MaxEnergy { get; private set; }
    public int AvailableEnergy { get; private set; }
    public int PendingDrawStepSkips { get; private set; }
    public int PendingEnergyRefreshStepSkips { get; private set; }
    public int PendingStandbyPhaseSkips { get; private set; }
    public int PendingBattlePhaseSkips { get; private set; }

    public IReadOnlyList<CardInstance> Deck => deck;
    public IReadOnlyList<CardInstance> Hand => hand;
    public IReadOnlyList<CardInstance> Graveyard => graveyard;
    public IReadOnlyList<CardInstance> MonsterZones => monsterZones;
    public IReadOnlyList<CardInstance> SpellTrapZones => spellTrapZones;
    public bool HasLost => Health <= 0;

    internal List<CardInstance> MutableDeck => deck;

    public PlayerState(
        int playerIndex,
        GameConfig config,
        List<CardInstance> initialDeck)
    {
        if (playerIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
        }

        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (initialDeck == null)
        {
            throw new ArgumentNullException(nameof(initialDeck));
        }

        PlayerIndex = playerIndex;
        Health = config.StartingPlayerHealth;
        MaxEnergy = config.BaseMaximumEnergy;
        AvailableEnergy = MaxEnergy;
        deck = initialDeck;
        hand = new List<CardInstance>();
        graveyard = new List<CardInstance>();
        monsterZones = new CardInstance[config.MonsterZoneCount];
        spellTrapZones = new CardInstance[config.SpellTrapZoneCount];
    }

    public int DrawCards(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        int drawnCount = 0;

        while (drawnCount < amount && deck.Count > 0)
        {
            int topIndex = deck.Count - 1;
            CardInstance card = deck[topIndex];
            deck.RemoveAt(topIndex);
            card.MoveToZone(CardZone.Hand, false);
            hand.Add(card);
            drawnCount++;
        }

        return drawnCount;
    }

    public void ResetAvailableEnergy()
    {
        AvailableEnergy = MaxEnergy;
    }

    public bool TrySpendEnergy(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (AvailableEnergy < amount)
        {
            return false;
        }

        AvailableEnergy -= amount;
        return true;
    }

    public void RestoreEnergy(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        AvailableEnergy = Math.Min(MaxEnergy, AvailableEnergy + amount);
    }

    public void IncreaseMaxEnergy(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        MaxEnergy += amount;
    }

    public void TakeDamage(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        Health = Math.Max(0, Health - amount);
    }

    public void Heal(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        Health += amount;
    }

    public void AddDrawStepSkips(int amount)
    {
        PendingDrawStepSkips = AddSkipCount(PendingDrawStepSkips, amount);
    }

    public void AddEnergyRefreshStepSkips(int amount)
    {
        PendingEnergyRefreshStepSkips =
            AddSkipCount(PendingEnergyRefreshStepSkips, amount);
    }

    public void AddStandbyPhaseSkips(int amount)
    {
        PendingStandbyPhaseSkips =
            AddSkipCount(PendingStandbyPhaseSkips, amount);
    }

    public void AddBattlePhaseSkips(int amount)
    {
        PendingBattlePhaseSkips =
            AddSkipCount(PendingBattlePhaseSkips, amount);
    }

    internal bool ConsumeDrawStepSkip()
    {
        if (PendingDrawStepSkips <= 0)
        {
            return false;
        }

        PendingDrawStepSkips--;
        return true;
    }

    internal bool ConsumeEnergyRefreshStepSkip()
    {
        if (PendingEnergyRefreshStepSkips <= 0)
        {
            return false;
        }

        PendingEnergyRefreshStepSkips--;
        return true;
    }

    internal bool ConsumeStandbyPhaseSkip()
    {
        if (PendingStandbyPhaseSkips <= 0)
        {
            return false;
        }

        PendingStandbyPhaseSkips--;
        return true;
    }

    internal bool ConsumeBattlePhaseSkip()
    {
        if (PendingBattlePhaseSkips <= 0)
        {
            return false;
        }

        PendingBattlePhaseSkips--;
        return true;
    }

    private static int AddSkipCount(int currentCount, int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        return checked(currentCount + amount);
    }

    internal CardInstance FindCardInHand(long instanceId)
    {
        return hand.Find(card => card.InstanceId == instanceId);
    }

    internal int FindMonsterZoneIndex(long instanceId)
    {
        for (int index = 0; index < monsterZones.Length; index++)
        {
            if (monsterZones[index] != null &&
                monsterZones[index].InstanceId == instanceId)
            {
                return index;
            }
        }

        return -1;
    }

    internal bool HasAnyMonster()
    {
        for (int index = 0; index < monsterZones.Length; index++)
        {
            if (monsterZones[index] != null)
            {
                return true;
            }
        }

        return false;
    }

    internal void PlaceMonsterFromHand(
        CardInstance monster,
        int zoneIndex,
        BattlePosition position)
    {
        hand.Remove(monster);
        monsterZones[zoneIndex] = monster;
        monster.Summon(position);
    }

    internal CardInstance TributeMonsterAt(int zoneIndex)
    {
        return MoveMonsterToGraveyard(zoneIndex);
    }

    internal CardInstance MoveMonsterToGraveyard(int zoneIndex)
    {
        CardInstance monster = monsterZones[zoneIndex];
        monsterZones[zoneIndex] = null;
        monster.MoveToZone(CardZone.Graveyard, false);
        monster.ClearAllocatedEnergy();
        graveyard.Add(monster);
        return monster;
    }

    internal void ResetTurnCardFlags()
    {
        for (int index = 0; index < monsterZones.Length; index++)
        {
            if (monsterZones[index] != null)
            {
                monsterZones[index].ResetTurnFlags();
            }
        }
    }
}
