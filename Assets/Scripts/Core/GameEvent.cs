using System;

public enum GameEventType
{
    CardDrawn,
    EnergyReset,
    PhaseChanged,
    TurnStarted
}

[Serializable]
public sealed class GameEvent
{
    public GameEventType Type { get; }
    public int PlayerIndex { get; }
    public long CardInstanceId { get; }
    public int Amount { get; }
    public TurnPhase Phase { get; }

    private GameEvent(
        GameEventType type,
        int playerIndex,
        long cardInstanceId,
        int amount,
        TurnPhase phase)
    {
        Type = type;
        PlayerIndex = playerIndex;
        CardInstanceId = cardInstanceId;
        Amount = amount;
        Phase = phase;
    }

    public static GameEvent CardDrawn(int playerIndex, long instanceId)
    {
        return new GameEvent(
            GameEventType.CardDrawn,
            playerIndex,
            instanceId,
            1,
            TurnPhase.DrawAndEnergy);
    }

    public static GameEvent EnergyReset(int playerIndex, int amount)
    {
        return new GameEvent(
            GameEventType.EnergyReset,
            playerIndex,
            0,
            amount,
            TurnPhase.DrawAndEnergy);
    }

    public static GameEvent PhaseChanged(int playerIndex, TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.PhaseChanged,
            playerIndex,
            0,
            0,
            phase);
    }

    public static GameEvent TurnStarted(int playerIndex, int turnNumber)
    {
        return new GameEvent(
            GameEventType.TurnStarted,
            playerIndex,
            0,
            turnNumber,
            TurnPhase.DrawAndEnergy);
    }
}
