using System;
using System.Collections.Generic;

public sealed class GameSession
{
    public GameState State { get; }
    public MatchLog Log { get; }

    private GameSession(GameState state, MatchLog log)
    {
        State = state;
        Log = log;
    }

    public static GameSession Create(
        GameConfig config,
        uint seed,
        IReadOnlyList<CardDefinition> playerOneDeck,
        IReadOnlyList<CardDefinition> playerTwoDeck)
    {
        GameState state = GameState.CreateNewMatch(
            config,
            seed,
            playerOneDeck,
            playerTwoDeck);

        return new GameSession(
            state,
            new MatchLog(state.Random.InitialSeed));
    }

    public ActionResult TryApply(GameAction action)
    {
        ActionResult result = GameRules.ApplyAction(State, action);

        if (result.Succeeded)
        {
            Log.Add(action);
        }

        return result;
    }
}
