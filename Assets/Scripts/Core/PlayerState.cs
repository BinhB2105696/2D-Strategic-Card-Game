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
