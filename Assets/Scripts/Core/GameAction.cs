using System;

public enum GameActionType
{
    ResolveDrawAndEnergy,
    AdvancePhase,
    EndTurn
}

[Serializable]
public sealed class GameAction
{
    public GameActionType Type { get; }
    public int PlayerIndex { get; }

    private GameAction(GameActionType type, int playerIndex)
    {
        Type = type;
        PlayerIndex = playerIndex;
    }

    public static GameAction ResolveDrawAndEnergy(int playerIndex)
    {
        return new GameAction(
            GameActionType.ResolveDrawAndEnergy,
            playerIndex);
    }

    public static GameAction AdvancePhase(int playerIndex)
    {
        return new GameAction(GameActionType.AdvancePhase, playerIndex);
    }

    public static GameAction EndTurn(int playerIndex)
    {
        return new GameAction(GameActionType.EndTurn, playerIndex);
    }

    public override string ToString()
    {
        return $"{Type} by Player {PlayerIndex}";
    }
}
