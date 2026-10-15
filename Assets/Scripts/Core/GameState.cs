using System;
using System.Collections.Generic;

[Serializable]
public sealed class GameState
{
    private readonly PlayerState[] players;
    private readonly Dictionary<string, int> effectUses =
        new Dictionary<string, int>();
    private readonly List<StatModifierApplication> statModifiers =
        new List<StatModifierApplication>();

    public GameConfig Config { get; }
    public SeededGameRandom Random { get; }
    public IReadOnlyList<PlayerState> Players => players;
    public int CurrentPlayerIndex { get; private set; }
    public int TurnNumber { get; private set; }
    public TurnPhase CurrentPhase { get; private set; }
    public RecoveryStep CurrentRecoveryStep { get; private set; }
    public long NextInstanceId { get; private set; }
    public bool IsFinished { get; private set; }
    public int WinnerPlayerIndex { get; private set; }
    public ChainState ActiveChain { get; private set; }
    public PendingTriggeredEffect PendingTriggeredEffect { get; private set; }
    public bool HasOpenResponseWindow => ActiveChain != null;
    public bool HasPendingTriggeredEffect => PendingTriggeredEffect != null;
    public int InteractionPlayerIndex => ActiveChain != null
        ? ActiveChain.PriorityPlayerIndex
        : PendingTriggeredEffect != null
            ? PendingTriggeredEffect.PlayerIndex
            : CurrentPlayerIndex;

    private GameState(GameConfig config, uint seed)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Random = new SeededGameRandom(seed);
        players = new PlayerState[2];
        TurnNumber = 1;
        CurrentPhase = TurnPhase.Recovery;
        CurrentRecoveryStep = RecoveryStep.Draw;
        NextInstanceId = 1;
        WinnerPlayerIndex = -1;
    }

    public static GameState CreateNewMatch(
        GameConfig config,
        uint seed,
        IReadOnlyList<CardDefinition> playerOneDeck,
        IReadOnlyList<CardDefinition> playerTwoDeck)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        ValidateDeck(config, playerOneDeck, nameof(playerOneDeck));
        ValidateDeck(config, playerTwoDeck, nameof(playerTwoDeck));

        GameState gameState = new GameState(config, seed);
        gameState.players[0] = gameState.CreatePlayer(0, playerOneDeck);
        gameState.players[1] = gameState.CreatePlayer(1, playerTwoDeck);

        gameState.Random.Shuffle(gameState.players[0].MutableDeck);
        gameState.Random.Shuffle(gameState.players[1].MutableDeck);
        gameState.CurrentPlayerIndex = gameState.Random.NextInt(2);

        gameState.players[0].DrawCards(config.StartingHandSize);
        gameState.players[1].DrawCards(config.StartingHandSize);
        return gameState;
    }

    internal void SetCurrentPhase(TurnPhase phase)
    {
        CurrentPhase = phase;
    }

    internal void SetRecoveryStep(RecoveryStep step)
    {
        CurrentRecoveryStep = step;
    }

    // Find the target player's next unresolved Draw Step, regardless of whose
    // turn the effect is activated on.
    public int GetNextDrawTurnForPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= players.Length)
            throw new ArgumentOutOfRangeException(nameof(playerIndex));

        if (playerIndex != CurrentPlayerIndex)
            return checked(TurnNumber + 1);

        return CurrentPhase == TurnPhase.Recovery &&
               CurrentRecoveryStep == RecoveryStep.Draw
            ? TurnNumber
            : checked(TurnNumber + 2);
    }

    internal void StartNextTurn()
    {
        foreach (PlayerState player in players)
            player.ClearPhaseSkipsForTurn(TurnNumber);

        CurrentPlayerIndex = 1 - CurrentPlayerIndex;
        TurnNumber++;
        CurrentPhase = TurnPhase.Recovery;
        CurrentRecoveryStep = RecoveryStep.Draw;
        players[CurrentPlayerIndex].ResetTurnCardFlags();
    }

    internal void OpenChain(ChainState chain)
    {
        ActiveChain = chain ?? throw new ArgumentNullException(nameof(chain));
    }

    internal void CloseChain()
    {
        ActiveChain = null;
    }

    internal void OpenTriggeredEffect(PendingTriggeredEffect pending)
    {
        PendingTriggeredEffect = pending ??
            throw new ArgumentNullException(nameof(pending));
    }

    internal void CloseTriggeredEffect()
    {
        PendingTriggeredEffect = null;
    }

    internal CardInstance FindCard(long instanceId)
    {
        for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
        {
            CardInstance card = players[playerIndex].FindCard(instanceId);
            if (card != null)
                return card;
        }

        return null;
    }

    internal void ApplyStatModifier(
        long sourceInstanceId,
        CardInstance target,
        int attackAmount,
        int defenseAmount,
        int expireAfterTurn = 0)
    {
        StatModifierApplication application = new StatModifierApplication(
            sourceInstanceId,
            target,
            attackAmount,
            defenseAmount,
            expireAfterTurn);
        application.Apply();
        statModifiers.Add(application);
    }

    internal void RemovePersistentModifiersFromSource(long sourceInstanceId)
    {
        RemoveModifiers(application =>
            application.SourceInstanceId == sourceInstanceId &&
            !application.IsTemporary);
    }

    internal void RemoveModifiersFromTarget(long targetInstanceId)
    {
        RemoveModifiers(application =>
            application.Target.InstanceId == targetInstanceId);
    }

    internal void ExpireTemporaryModifiers(int completedTurn)
    {
        RemoveModifiers(application =>
            application.IsTemporary &&
            application.ExpireAfterTurn <= completedTurn);
    }

    internal bool HasModifierFromSourceToTarget(
        long sourceInstanceId,
        long targetInstanceId)
    {
        return statModifiers.Exists(application =>
            !application.IsTemporary &&
            application.SourceInstanceId == sourceInstanceId &&
            application.Target.InstanceId == targetInstanceId);
    }

    private void RemoveModifiers(
        Predicate<StatModifierApplication> predicate)
    {
        for (int index = statModifiers.Count - 1; index >= 0; index--)
        {
            StatModifierApplication application = statModifiers[index];
            if (!predicate(application))
                continue;

            application.Remove();
            statModifiers.RemoveAt(index);
        }
    }

    internal void EndMatch(int winnerPlayerIndex)
    {
        IsFinished = true;
        WinnerPlayerIndex = winnerPlayerIndex;
    }

    internal bool CanUseEffect(
        EffectDefinition effect,
        int playerIndex,
        CardInstance source)
    {
        if (effect.MaxUses == 0)
            return true;

        return !effectUses.TryGetValue(
            EffectUseKey(effect, playerIndex, source), out int count) ||
            count < effect.MaxUses;
    }

    internal void RecordEffectUse(
        EffectDefinition effect,
        int playerIndex,
        CardInstance source)
    {
        if (effect.MaxUses == 0)
            return;

        string key = EffectUseKey(effect, playerIndex, source);
        effectUses.TryGetValue(key, out int count);
        effectUses[key] = count + 1;
    }

    private string EffectUseKey(
        EffectDefinition effect,
        int playerIndex,
        CardInstance source)
    {
        string identity = effect.CountBy == EffectUsageScope.PerCardId
            ? "card:" + source.Definition.CardId
            : "instance:" + source.InstanceId;
        string period = effect.Reset == EffectUsageReset.PerTurn
            ? "turn:" + TurnNumber
            : "match";
        return playerIndex + "/" + period + "/" + identity +
            "/" + effect.EffectId;
    }

    private PlayerState CreatePlayer(
        int playerIndex,
        IReadOnlyList<CardDefinition> definitions)
    {
        List<CardInstance> instances =
            new List<CardInstance>(definitions.Count);

        for (int index = 0; index < definitions.Count; index++)
        {
            instances.Add(new CardInstance(
                NextInstanceId,
                playerIndex,
                definitions[index]));
            NextInstanceId++;
        }

        return new PlayerState(playerIndex, Config, instances);
    }

    private static void ValidateDeck(
        GameConfig config,
        IReadOnlyList<CardDefinition> deck,
        string parameterName)
    {
        if (deck == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (deck.Count < config.MinimumDeckSize ||
            deck.Count > config.MaximumDeckSize)
        {
            throw new ArgumentException(
                $"Deck size must be between {config.MinimumDeckSize} and " +
                $"{config.MaximumDeckSize} cards.",
                parameterName);
        }

        for (int index = 0; index < deck.Count; index++)
        {
            if (deck[index] == null)
            {
                throw new ArgumentException(
                    $"Deck contains a null definition at index {index}.",
                    parameterName);
            }
        }
    }
}
