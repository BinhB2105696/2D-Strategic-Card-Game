using System;

public enum GameEventType
{
    CardDrawn,
    DrawStepSkipped,
    RecoveryStepChanged,
    EnergyRefreshed,
    EnergyRefreshSkipped,
    StandbyPhaseSkipped,
    BattlePhaseSkipped,
    PhaseChanged,
    TurnStarted,
    MonsterTributed,
    MonsterSummoned,
    EnergySpent,
    EnergyAllocated,
    BattlePositionChanged,
    AttackDeclared,
    MonsterDamaged,
    MonsterDestroyed,
    PlayerDamaged,
    MatchEnded
}

[Serializable]
public sealed class GameEvent
{
    public GameEventType Type { get; }
    public int PlayerIndex { get; }
    public long CardInstanceId { get; }
    public long TargetInstanceId { get; }
    public int Amount { get; }
    public TurnPhase Phase { get; }

    private GameEvent(
        GameEventType type,
        int playerIndex,
        long cardInstanceId,
        long targetInstanceId,
        int amount,
        TurnPhase phase)
    {
        Type = type;
        PlayerIndex = playerIndex;
        CardInstanceId = cardInstanceId;
        TargetInstanceId = targetInstanceId;
        Amount = amount;
        Phase = phase;
    }

    public static GameEvent CardDrawn(int playerIndex, long instanceId)
    {
        return new GameEvent(
            GameEventType.CardDrawn,
            playerIndex,
            instanceId,
            0,
            1,
            TurnPhase.Recovery);
    }

    public static GameEvent DrawStepSkipped(int playerIndex)
    {
        return new GameEvent(
            GameEventType.DrawStepSkipped,
            playerIndex,
            0,
            0,
            0,
            TurnPhase.Recovery);
    }

    public static GameEvent RecoveryStepChanged(
        int playerIndex,
        RecoveryStep step)
    {
        return new GameEvent(
            GameEventType.RecoveryStepChanged,
            playerIndex,
            0,
            0,
            (int)step,
            TurnPhase.Recovery);
    }

    public static GameEvent EnergyRefreshed(int playerIndex, int amount)
    {
        return new GameEvent(
            GameEventType.EnergyRefreshed,
            playerIndex,
            0,
            0,
            amount,
            TurnPhase.Recovery);
    }

    public static GameEvent EnergyRefreshSkipped(int playerIndex)
    {
        return new GameEvent(
            GameEventType.EnergyRefreshSkipped,
            playerIndex,
            0,
            0,
            0,
            TurnPhase.Recovery);
    }

    public static GameEvent StandbyPhaseSkipped(int playerIndex)
    {
        return new GameEvent(
            GameEventType.StandbyPhaseSkipped,
            playerIndex,
            0,
            0,
            0,
            TurnPhase.Standby);
    }

    public static GameEvent BattlePhaseSkipped(int playerIndex)
    {
        return new GameEvent(
            GameEventType.BattlePhaseSkipped,
            playerIndex,
            0,
            0,
            0,
            TurnPhase.Battle);
    }

    public static GameEvent PhaseChanged(int playerIndex, TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.PhaseChanged,
            playerIndex,
            0,
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
            0,
            turnNumber,
            TurnPhase.Recovery);
    }

    public static GameEvent MonsterTributed(
        int playerIndex,
        long instanceId)
    {
        return new GameEvent(
            GameEventType.MonsterTributed,
            playerIndex,
            instanceId,
            0,
            0,
            TurnPhase.Main);
    }

    public static GameEvent MonsterSummoned(
        int playerIndex,
        long instanceId,
        int zoneIndex)
    {
        return new GameEvent(
            GameEventType.MonsterSummoned,
            playerIndex,
            instanceId,
            0,
            zoneIndex,
            TurnPhase.Main);
    }

    public static GameEvent EnergySpent(int playerIndex, int amount)
    {
        return new GameEvent(
            GameEventType.EnergySpent,
            playerIndex,
            0,
            0,
            amount,
            TurnPhase.Main);
    }

    public static GameEvent EnergyAllocated(
        int playerIndex,
        long instanceId,
        int amount)
    {
        return new GameEvent(
            GameEventType.EnergyAllocated,
            playerIndex,
            instanceId,
            0,
            amount,
            TurnPhase.Main);
    }

    public static GameEvent BattlePositionChanged(
        int playerIndex,
        long instanceId,
        BattlePosition position)
    {
        return new GameEvent(
            GameEventType.BattlePositionChanged,
            playerIndex,
            instanceId,
            0,
            (int)position,
            TurnPhase.Main);
    }

    public static GameEvent AttackDeclared(
        int playerIndex,
        long attackerInstanceId,
        long targetInstanceId)
    {
        return new GameEvent(
            GameEventType.AttackDeclared,
            playerIndex,
            attackerInstanceId,
            targetInstanceId,
            0,
            TurnPhase.Battle);
    }

    public static GameEvent MonsterDamaged(
        int playerIndex,
        long instanceId,
        int damage)
    {
        return new GameEvent(
            GameEventType.MonsterDamaged,
            playerIndex,
            instanceId,
            0,
            damage,
            TurnPhase.Battle);
    }

    public static GameEvent MonsterDestroyed(
        int playerIndex,
        long instanceId)
    {
        return new GameEvent(
            GameEventType.MonsterDestroyed,
            playerIndex,
            instanceId,
            0,
            0,
            TurnPhase.Battle);
    }

    public static GameEvent PlayerDamaged(int playerIndex, int damage)
    {
        return new GameEvent(
            GameEventType.PlayerDamaged,
            playerIndex,
            0,
            0,
            damage,
            TurnPhase.Battle);
    }

    public static GameEvent MatchEnded(int winnerPlayerIndex)
    {
        return new GameEvent(
            GameEventType.MatchEnded,
            winnerPlayerIndex,
            0,
            0,
            0,
            TurnPhase.End);
    }
}
