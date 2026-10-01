using System;
using System.Collections.Generic;

[Serializable]
public sealed class GameState
{
    private readonly PlayerState[] players;

    public GameConfig Config { get; }
    public SeededGameRandom Random { get; }
    public IReadOnlyList<PlayerState> Players => players;
    public int CurrentPlayerIndex { get; private set; }
    public int TurnNumber { get; private set; }
    public TurnPhase CurrentPhase { get; private set; }
    public long NextInstanceId { get; private set; }

    private GameState(GameConfig config, uint seed)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Random = new SeededGameRandom(seed);
        players = new PlayerState[2];
        TurnNumber = 1;
        CurrentPhase = TurnPhase.DrawAndEnergy;
        NextInstanceId = 1;
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

    internal void StartNextTurn()
    {
        CurrentPlayerIndex = 1 - CurrentPlayerIndex;
        TurnNumber++;
        CurrentPhase = TurnPhase.DrawAndEnergy;
        players[CurrentPlayerIndex].ResetTurnCardFlags();
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
