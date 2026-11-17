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

    private PlayerState(PlayerState source, SearchCloneContext context)
    {
        PlayerIndex = source.PlayerIndex;
        Health = source.Health;
        MaxEnergy = source.MaxEnergy;
        AvailableEnergy = source.AvailableEnergy;
        LastFieldSpellActivationTurn = source.LastFieldSpellActivationTurn;
        deck = new List<CardInstance>();
        hand = new List<CardInstance>();
        graveyard = new List<CardInstance>();
        foreach (CardInstance card in source.deck) deck.Add(context.Card(card));
        foreach (CardInstance card in source.hand) hand.Add(context.Card(card));
        foreach (CardInstance card in source.graveyard) graveyard.Add(context.Card(card));
        monsterZones = new CardInstance[source.monsterZones.Length];
        spellTrapZones = new CardInstance[source.spellTrapZones.Length];
        for (int i = 0; i < monsterZones.Length; i++) monsterZones[i] = context.Card(source.monsterZones[i]);
        for (int i = 0; i < spellTrapZones.Length; i++) spellTrapZones[i] = context.Card(source.spellTrapZones[i]);
        fieldSpell = context.Card(source.fieldSpell);
        skippedDrawTurns.UnionWith(source.skippedDrawTurns);
        skippedEnergyRefreshTurns.UnionWith(source.skippedEnergyRefreshTurns);
        skippedStandbyTurns.UnionWith(source.skippedStandbyTurns);
        skippedBattleTurns.UnionWith(source.skippedBattleTurns);
    }

    internal PlayerState CopyForSearch(SearchCloneContext context) => new PlayerState(this, context);

    // Called only on a search fork. Unknown identities/order are rebuilt from a public prior.
    internal void SampleHiddenForSearch(int observer, IReadOnlyDictionary<long, CardDefinition> known,
        CardDefinition[] catalog, SeededGameRandom random, ref long syntheticId, IReadOnlyDictionary<string, int> ownRemainingDeck = null)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var seen = new HashSet<long>();
        Action<CardInstance> countKnown = card => {
            if (card == null || !known.TryGetValue(card.InstanceId, out CardDefinition definition) || !seen.Add(card.InstanceId)) return;
            counts.TryGetValue(definition.CardId, out int used); counts[definition.CardId] = used + 1;
        };
        foreach (CardInstance card in deck) countKnown(card);
        foreach (CardInstance card in hand) countKnown(card);
        foreach (CardInstance card in graveyard) countKnown(card);
        foreach (CardInstance card in monsterZones) countKnown(card);
        foreach (CardInstance card in spellTrapZones) countKnown(card);
        countKnown(fieldSpell);
        Func<CardDefinition, bool> canSample = definition => !counts.TryGetValue(definition.CardId, out int used) || used < definition.MaxCopiesPerDeck;
        Func<Func<CardDefinition, bool>, CardDefinition> pick = filter => {
            var choices = new List<CardDefinition>();
            foreach (CardDefinition definition in catalog) if (canSample(definition) && filter(definition)) choices.Add(definition);
            if (choices.Count == 0) throw new InvalidOperationException("Public search prior cannot fill the hidden card slots.");
            CardDefinition selected = choices[random.NextInt(choices.Count)];
            counts.TryGetValue(selected.CardId, out int used); counts[selected.CardId] = used + 1; return selected;
        };
        for (int slot = 0; slot < monsterZones.Length; slot++)
            if (monsterZones[slot] != null && !known.ContainsKey(monsterZones[slot].InstanceId))
                throw new InvalidOperationException("Face-down monster search requires an explicit public observation model.");
        for (int slot = 0; slot < spellTrapZones.Length; slot++)
        {
            CardInstance previous = spellTrapZones[slot];
            if (previous == null || known.ContainsKey(previous.InstanceId)) continue;
            var replacement = new CardInstance(NextSynthetic(known, ref syntheticId), PlayerIndex,
                pick(definition => definition.Category == CardCategory.Trap));
            replacement.MoveToZone(CardZone.SpellTrapZone, true);
            // Set age is public action history, not a hidden card identity.
            replacement.SetFaceDownInSpellTrapZone(previous.SetTurnNumber);
            spellTrapZones[slot] = replacement;
        }
        if (PlayerIndex != observer)
        {
            int count = hand.Count; hand.Clear();
            for (int i = 0; i < count; i++)
            {
                var replacement = new CardInstance(NextSynthetic(known, ref syntheticId), PlayerIndex, pick(definition => true));
                replacement.MoveToZone(CardZone.Hand, false); hand.Add(replacement);
            }
        }
        var revealedDeck = new List<CardInstance>();
        var revealedTypes = new HashSet<string>(StringComparer.Ordinal);
        var revealedKinds = new HashSet<int>();
        foreach (CardInstance card in deck)
            if (known.TryGetValue(card.InstanceId, out CardDefinition definition))
            { revealedDeck.Add(card); revealedTypes.Add(definition.CardId); revealedKinds.Add(SearchCardKind(definition)); }
        revealedDeck.Sort((left, right) => {
            int compare = StringComparer.Ordinal.Compare(left.Definition.CardId, right.Definition.CardId);
            return compare != 0 ? compare : left.InstanceId.CompareTo(right.InstanceId);
        });
        int unknownCount = deck.Count - revealedDeck.Count;
        deck.Clear(); deck.AddRange(revealedDeck);
        var ownedBag = new List<CardDefinition>();
        if (ownRemainingDeck != null)
        {
            var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in ownRemainingDeck) remaining[pair.Key] = pair.Value;
            foreach (CardInstance card in revealedDeck)
            {
                if (!remaining.TryGetValue(card.Definition.CardId, out int count) || count < 1)
                    throw new InvalidOperationException("Revealed deck choices conflict with known own deck composition.");
                remaining[card.Definition.CardId] = count - 1;
            }
            foreach (CardDefinition definition in catalog)
                if (remaining.TryGetValue(definition.CardId, out int count))
                    for (int copy = 0; copy < count; copy++) ownedBag.Add(definition);
            if (ownedBag.Count != unknownCount) throw new InvalidOperationException("Known own deck size does not match observed deck count.");
        }
        for (int i = 0; i < unknownCount; i++)
        {
            CardDefinition definition = ownRemainingDeck != null ? ownedBag[i]
                : pick(candidate => !revealedKinds.Contains(SearchCardKind(candidate)) || revealedTypes.Contains(candidate.CardId));
            deck.Add(new CardInstance(NextSynthetic(known, ref syntheticId), PlayerIndex, definition));
        }
        random.Shuffle(deck);
    }

    private static int SearchCardKind(CardDefinition card) => !card.IsMonster ? 2 : card.MonsterRank == MonsterRank.Basic ? 0 : 1;
    private static long NextSynthetic(IReadOnlyDictionary<long, CardDefinition> known, ref long next)
    { while (known.ContainsKey(next)) next--; return next--; }

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
