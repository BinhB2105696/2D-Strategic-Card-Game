using System;
using System.Collections.Generic;

[Serializable]
public sealed class BotCardView
{
    public bool IsKnown { get; }
    public long InstanceId { get; }
    public int OwnerPlayerIndex { get; }
    public string CardId { get; }
    public string CardName { get; }
    public CardCategory Category { get; }
    public MonsterRank MonsterRank { get; }
    public SpellType SpellType { get; }
    public int AttackPriority { get; }
    public TrapType TrapType { get; }
    public CardSpeed Speed { get; }
    public CardZone Zone { get; }
    public BattlePosition BattlePosition { get; }
    public int CurrentHealth { get; }
    public int MaximumHealth { get; }
    public int CurrentAttack { get; }
    public int CurrentDefense { get; }
    public int AllocatedEnergy { get; }
    public int AttackEnergyRequirement { get; }
    public int TributeCount { get; }
    public bool IsFaceDown { get; }

    internal BotCardView(CardInstance card, bool revealFaceDownDetails = true)
    {
        if (card == null)
            throw new ArgumentNullException(nameof(card));

        CardDefinition definition = card.Definition;
        IsKnown = revealFaceDownDetails || !card.IsFaceDown;
        InstanceId = card.InstanceId;
        OwnerPlayerIndex = card.OwnerPlayerIndex;
        CardId = IsKnown ? definition.CardId : string.Empty;
        CardName = IsKnown ? definition.CardName : "Hidden Card";
        Category = IsKnown ? definition.Category : default(CardCategory);
        MonsterRank = IsKnown
            ? definition.MonsterRank
            : default(MonsterRank);
        SpellType = IsKnown ? definition.SpellType : default(SpellType);
        AttackPriority = card.AttackPriority;
        TrapType = IsKnown ? definition.TrapType : default(TrapType);
        Speed = IsKnown ? definition.Speed : default(CardSpeed);
        Zone = card.Zone;
        BattlePosition = card.BattlePosition;
        CurrentHealth = IsKnown ? card.CurrentHealth : 0;
        MaximumHealth = IsKnown ? card.MaximumHealth : 0;
        CurrentAttack = IsKnown ? card.CurrentAttack : 0;
        CurrentDefense = IsKnown ? card.CurrentDefense : 0;
        AllocatedEnergy = IsKnown ? card.AllocatedEnergy : 0;
        AttackEnergyRequirement = IsKnown
            ? definition.AttackEnergyRequirement
            : 0;
        TributeCount = IsKnown ? definition.TributeCount : 0;
        IsFaceDown = card.IsFaceDown;
    }
}

[Serializable]
public sealed class BotPlayerView
{
    private readonly IReadOnlyList<BotCardView> hand;
    private readonly IReadOnlyList<BotCardView> monsterZones;
    private readonly IReadOnlyList<BotCardView> spellTrapZones;
    private readonly IReadOnlyList<BotCardView> graveyard;

    public int PlayerIndex { get; }
    public int Health { get; }
    public int MaxEnergy { get; }
    public int AvailableEnergy { get; }
    public int DeckCount { get; }
    public int DeckExhaustionLevel { get; }
    public int PendingDeckExhaustionDamage { get; }
    public int HandCount { get; }
    public int GraveyardCount { get; }
    public IReadOnlyList<BotCardView> Hand => hand;
    public IReadOnlyList<BotCardView> MonsterZones => monsterZones;
    public IReadOnlyList<BotCardView> SpellTrapZones => spellTrapZones;
    public IReadOnlyList<BotCardView> Graveyard => graveyard;
    public BotCardView FieldSpell { get; }

    internal BotPlayerView(PlayerState player, bool isViewingPlayer)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));

        PlayerIndex = player.PlayerIndex;
        Health = player.Health;
        MaxEnergy = player.MaxEnergy;
        AvailableEnergy = player.AvailableEnergy;
        DeckCount = player.Deck.Count;
        DeckExhaustionLevel = player.DeckExhaustionLevel;
        PendingDeckExhaustionDamage = player.PendingDeckExhaustionDamage;
        HandCount = player.Hand.Count;
        GraveyardCount = player.Graveyard.Count;
        hand = isViewingPlayer
            ? CopyCards(player.Hand, true)
            : Array.AsReadOnly(new BotCardView[0]);
        monsterZones = CopyCards(
            player.MonsterZones,
            isViewingPlayer);
        spellTrapZones = CopyCards(
            player.SpellTrapZones,
            isViewingPlayer);
        graveyard = isViewingPlayer
            ? CopyCards(player.Graveyard, true)
            : Array.AsReadOnly(new BotCardView[0]);
        FieldSpell = player.FieldSpell == null
            ? null
            : new BotCardView(player.FieldSpell, true);
    }

    private static IReadOnlyList<BotCardView> CopyCards(
        IReadOnlyList<CardInstance> cards,
        bool revealFaceDownDetails)
    {
        BotCardView[] copy = new BotCardView[cards.Count];
        for (int index = 0; index < cards.Count; index++)
        {
            copy[index] = cards[index] == null
                ? null
                : new BotCardView(
                    cards[index],
                    revealFaceDownDetails);
        }
        return Array.AsReadOnly(copy);
    }
}

// Immutable snapshot for bot decisions. Bots never receive the mutable
// GameState, so they cannot bypass GameRules or alter the match directly.
[Serializable]
public sealed class BotGameStateView
{
    private readonly IReadOnlyList<BotPlayerView> players;
    private readonly Dictionary<long, BotCardView> cardsByInstanceId;

    public int ViewingPlayerIndex { get; }
    public int CurrentPlayerIndex { get; }
    public int InteractionPlayerIndex { get; }
    public int TurnNumber { get; }
    public TurnPhase CurrentPhase { get; }
    public bool IsFinished { get; }
    public int WinnerPlayerIndex { get; }
    public bool HasOpenResponseWindow { get; }
    public bool HasPendingTriggeredEffect { get; }
    public int MainStartLpDeficit { get; private set; }
    public IReadOnlyList<BotCardView> ChoiceCards => choiceCards;
    private readonly List<BotCardView> choiceCards = new List<BotCardView>();
    public IReadOnlyList<BotPlayerView> Players => players;

    private BotGameStateView(GameState state, int viewingPlayerIndex)
    {
        ViewingPlayerIndex = viewingPlayerIndex;
        CurrentPlayerIndex = state.CurrentPlayerIndex;
        InteractionPlayerIndex = state.InteractionPlayerIndex;
        TurnNumber = state.TurnNumber;
        CurrentPhase = state.CurrentPhase;
        IsFinished = state.IsFinished;
        WinnerPlayerIndex = state.WinnerPlayerIndex;
        HasOpenResponseWindow = state.HasOpenResponseWindow;
        HasPendingTriggeredEffect = state.HasPendingTriggeredEffect;

        BotPlayerView[] playerCopies =
            new BotPlayerView[state.Players.Count];
        cardsByInstanceId = new Dictionary<long, BotCardView>();
        for (int index = 0; index < state.Players.Count; index++)
        {
            playerCopies[index] = new BotPlayerView(
                state.Players[index],
                index == viewingPlayerIndex);
            IndexCards(playerCopies[index]);
        }
        players = Array.AsReadOnly(playerCopies);
    }

    public static BotGameStateView Create(
        GameState state,
        int viewingPlayerIndex)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (viewingPlayerIndex < 0 ||
            viewingPlayerIndex >= state.Players.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(viewingPlayerIndex));
        }

        var view = new BotGameStateView(state, viewingPlayerIndex);
        view.MainStartLpDeficit = state.LpDeficitAtMainStart(viewingPlayerIndex);
        foreach (CardInstance card in CardExpansion.OwnChoiceCards(state, viewingPlayerIndex))
        {
            var choice = new BotCardView(card, true);
            view.choiceCards.Add(choice);
            view.cardsByInstanceId[card.InstanceId] = choice;
        }
        return view;
    }

    public BotCardView FindCard(long instanceId)
    {
        cardsByInstanceId.TryGetValue(instanceId, out BotCardView card);
        return card;
    }

    private void IndexCards(BotPlayerView player)
    {
        IndexCards(player.Hand);
        IndexCards(player.MonsterZones);
        IndexCards(player.SpellTrapZones);
        IndexCards(player.Graveyard);
        if (player.FieldSpell != null)
            cardsByInstanceId[player.FieldSpell.InstanceId] =
                player.FieldSpell;
    }

    private void IndexCards(IReadOnlyList<BotCardView> cards)
    {
        for (int index = 0; index < cards.Count; index++)
        {
            BotCardView card = cards[index];
            if (card != null)
                cardsByInstanceId[card.InstanceId] = card;
        }
    }
}
