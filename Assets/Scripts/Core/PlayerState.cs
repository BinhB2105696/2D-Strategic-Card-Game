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
    private CardInstance fieldSpell;
    private readonly HashSet<int> skippedDrawTurns = new HashSet<int>();
    private readonly HashSet<int> skippedEnergyRefreshTurns = new HashSet<int>();
    private readonly HashSet<int> skippedStandbyTurns = new HashSet<int>();
    private readonly HashSet<int> skippedBattleTurns = new HashSet<int>();

    public int PlayerIndex { get; }
    public int Health { get; private set; }
    public int MaxEnergy { get; private set; }
    public int AvailableEnergy { get; private set; }
    public IReadOnlyList<CardInstance> Deck => deck;
    public IReadOnlyList<CardInstance> Hand => hand;
    public IReadOnlyList<CardInstance> Graveyard => graveyard;
    public IReadOnlyList<CardInstance> MonsterZones => monsterZones;
    public IReadOnlyList<CardInstance> SpellTrapZones => spellTrapZones;
    public CardInstance FieldSpell => fieldSpell;
    public int LastFieldSpellActivationTurn { get; private set; } = -1;
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

    internal CardInstance MoveDeckCardToHand(long instanceId)
    {
        CardInstance card = deck.Find(item => item.InstanceId == instanceId);
        if (card == null)
            return null;

        deck.Remove(card);
        card.MoveToZone(CardZone.Hand, false);
        hand.Add(card);
        return card;
    }

    internal CardInstance MoveGraveyardCardToHand(long instanceId)
    {
        CardInstance card = graveyard.Find(item => item.InstanceId == instanceId);
        if (card == null)
            return null;

        graveyard.Remove(card);
        card.ResetHealthToBase();
        card.MoveToZone(CardZone.Hand, false);
        hand.Add(card);
        return card;
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

    // A skip belongs to an exact match turn. Repeating it for that turn does
    // not create another skip on a later turn.
    public void ScheduleDrawStepSkip(int turnNumber) =>
        ScheduleSkip(skippedDrawTurns, turnNumber);

    public void ScheduleEnergyRefreshStepSkip(int turnNumber) =>
        ScheduleSkip(skippedEnergyRefreshTurns, turnNumber);

    public void ScheduleStandbyPhaseSkip(int turnNumber) =>
        ScheduleSkip(skippedStandbyTurns, turnNumber);

    public void ScheduleBattlePhaseSkip(int turnNumber) =>
        ScheduleSkip(skippedBattleTurns, turnNumber);

    public bool IsDrawStepSkipScheduled(int turnNumber) =>
        skippedDrawTurns.Contains(turnNumber);

    public bool IsEnergyRefreshStepSkipScheduled(int turnNumber) =>
        skippedEnergyRefreshTurns.Contains(turnNumber);

    public bool IsStandbyPhaseSkipScheduled(int turnNumber) =>
        skippedStandbyTurns.Contains(turnNumber);

    public bool IsBattlePhaseSkipScheduled(int turnNumber) =>
        skippedBattleTurns.Contains(turnNumber);

    internal bool ConsumeDrawStepSkip(int turnNumber) =>
        skippedDrawTurns.Remove(turnNumber);

    internal bool ConsumeEnergyRefreshStepSkip(int turnNumber) =>
        skippedEnergyRefreshTurns.Remove(turnNumber);

    internal bool ConsumeStandbyPhaseSkip(int turnNumber) =>
        skippedStandbyTurns.Remove(turnNumber);

    internal bool ConsumeBattlePhaseSkip(int turnNumber) =>
        skippedBattleTurns.Remove(turnNumber);

    internal void ClearPhaseSkipsForTurn(int turnNumber)
    {
        skippedDrawTurns.Remove(turnNumber);
        skippedEnergyRefreshTurns.Remove(turnNumber);
        skippedStandbyTurns.Remove(turnNumber);
        skippedBattleTurns.Remove(turnNumber);
    }

    private static void ScheduleSkip(HashSet<int> turns, int turnNumber)
    {
        if (turnNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(turnNumber));

        turns.Add(turnNumber);
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

    internal int FindSpellTrapZoneIndex(long instanceId)
    {
        for (int index = 0; index < spellTrapZones.Length; index++)
        {
            if (spellTrapZones[index] != null &&
                spellTrapZones[index].InstanceId == instanceId)
            {
                return index;
            }
        }

        return -1;
    }

    internal CardInstance FindCard(long instanceId)
    {
        CardInstance card = deck.Find(item => item.InstanceId == instanceId) ??
            hand.Find(item => item.InstanceId == instanceId) ??
            graveyard.Find(item => item.InstanceId == instanceId);
        if (card != null)
            return card;

        int monsterIndex = FindMonsterZoneIndex(instanceId);
        if (monsterIndex >= 0)
            return monsterZones[monsterIndex];

        int spellTrapIndex = FindSpellTrapZoneIndex(instanceId);
        if (spellTrapIndex >= 0)
            return spellTrapZones[spellTrapIndex];

        return fieldSpell != null && fieldSpell.InstanceId == instanceId
            ? fieldSpell
            : null;
    }

    internal bool IsSpellTrapZoneOpen(int zoneIndex)
    {
        return zoneIndex >= 0 &&
            zoneIndex < spellTrapZones.Length &&
            spellTrapZones[zoneIndex] == null;
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

    internal void MoveHandCardToGraveyard(CardInstance card)
    {
        if (card == null || !hand.Remove(card))
        {
            throw new InvalidOperationException(
                "The card must be in this player's hand.");
        }

        card.MoveToZone(CardZone.Graveyard, false);
        graveyard.Add(card);
    }

    internal void PlaceSpellTrapFromHand(
        CardInstance card,
        int zoneIndex,
        bool faceDown)
    {
        if (card == null || !hand.Remove(card))
        {
            throw new InvalidOperationException(
                "The card must be in this player's hand.");
        }

        if (!IsSpellTrapZoneOpen(zoneIndex))
        {
            throw new InvalidOperationException(
                "The selected Spell/Trap Zone must be empty.");
        }

        spellTrapZones[zoneIndex] = card;
        card.MoveToZone(CardZone.SpellTrapZone, faceDown);
    }

    internal void SetTrapFromHand(
        CardInstance card,
        int zoneIndex,
        int turnNumber)
    {
        PlaceSpellTrapFromHand(card, zoneIndex, true);
        card.SetFaceDownInSpellTrapZone(turnNumber);
    }

    internal CardInstance PlaceFieldSpellFromHand(CardInstance card)
    {
        if (card == null || !hand.Remove(card))
        {
            throw new InvalidOperationException(
                "The Field Spell must be in this player's hand.");
        }

        CardInstance replaced = fieldSpell;
        if (replaced != null)
        {
            replaced.MoveToZone(CardZone.Graveyard, false);
            graveyard.Add(replaced);
        }

        fieldSpell = card;
        card.MoveToZone(CardZone.FieldZone, false);
        return replaced;
    }

    internal CardInstance MoveFieldSpellToGraveyard()
    {
        if (fieldSpell == null)
            return null;

        CardInstance card = fieldSpell;
        fieldSpell = null;
        card.MoveToZone(CardZone.Graveyard, false);
        graveyard.Add(card);
        return card;
    }

    internal void MarkFieldSpellActivated(int turnNumber)
    {
        LastFieldSpellActivationTurn = turnNumber;
    }

    internal CardInstance MoveSpellTrapToGraveyard(int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex >= spellTrapZones.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(zoneIndex));
        }

        CardInstance card = spellTrapZones[zoneIndex];

        if (card == null)
        {
            throw new InvalidOperationException(
                "The selected Spell/Trap Zone is empty.");
        }

        spellTrapZones[zoneIndex] = null;
        card.MoveToZone(CardZone.Graveyard, false);
        graveyard.Add(card);
        return card;
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
