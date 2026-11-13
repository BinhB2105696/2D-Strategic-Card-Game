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
    AllocatedEnergyRemoved,
    BattlePositionChanged,
    AttackDeclared,
    MonsterDamaged,
    MonsterDestroyed,
    PlayerDamaged,
    CardPlacedInSpellTrapZone,
    CardSet,
    SpellActivated,
    TrapActivated,
    FieldSpellActivated,
    EquipAttached,
    ChainStarted,
    ChainLinkAdded,
    ResponsePassed,
    CardActivationNegated,
    MonsterAttackModified,
    MonsterDefenseModified,
    MonsterHealthModified,
    FieldMaintenancePaid,
    EffectResolved,
    CardSentToGraveyard,
    CardAddedToHand,
    DeckShuffled,
    TriggeredEffectOffered,
    TriggeredEffectDeclined,
    MatchEnded,
    MonsterMaximumHealthModified,
    EnergyRestored,
    CardRevealed
}

[Serializable]
public sealed class GameEvent
{
    public static GameEvent EnergyRestored(int player, int amount, TurnPhase phase)
        => new GameEvent(GameEventType.EnergyRestored, player, 0, 0, amount, phase);
    public static GameEvent CardRevealed(int player, long card, TurnPhase phase)
        => new GameEvent(GameEventType.CardRevealed, player, card, 0, 0, phase);

    public GameEventType Type { get; }
    public int PlayerIndex { get; }
    public long CardInstanceId { get; }
    public long TargetInstanceId { get; }
    public int Amount { get; }
    public TurnPhase Phase { get; }

    public static GameEvent MonsterMaximumHealthModified(int playerIndex,
        long instanceId, int amount, TurnPhase phase) =>
        new GameEvent(GameEventType.MonsterMaximumHealthModified,
            playerIndex, instanceId, 0, amount, phase);

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

    public static GameEvent CardDrawn(
        int playerIndex,
        long instanceId,
        TurnPhase phase = TurnPhase.Recovery)
    {
        return new GameEvent(
            GameEventType.CardDrawn,
            playerIndex,
            instanceId,
            0,
            1,
            phase);
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

    public static GameEvent EnergySpent(
        int playerIndex,
        int amount,
        TurnPhase phase = TurnPhase.Main)
    {
        return new GameEvent(
            GameEventType.EnergySpent,
            playerIndex,
            0,
            0,
            amount,
            phase);
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

    public static GameEvent AllocatedEnergyRemoved(
        int playerIndex,
        long instanceId,
        int amount,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.AllocatedEnergyRemoved,
            playerIndex,
            instanceId,
            0,
            amount,
            phase);
    }

    public static GameEvent CardAddedToHand(
        int playerIndex,
        long instanceId,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.CardAddedToHand,
            playerIndex,
            instanceId,
            0,
            1,
            phase);
    }

    public static GameEvent DeckShuffled(int playerIndex, TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.DeckShuffled,
            playerIndex,
            0,
            0,
            0,
            phase);
    }

    public static GameEvent TriggeredEffectOffered(
        int playerIndex,
        long sourceInstanceId,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.TriggeredEffectOffered,
            playerIndex,
            sourceInstanceId,
            0,
            0,
            phase);
    }

    public static GameEvent TriggeredEffectDeclined(
        int playerIndex,
        long sourceInstanceId,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.TriggeredEffectDeclined,
            playerIndex,
            sourceInstanceId,
            0,
            0,
            phase);
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

    public static GameEvent EffectResolved(
        int playerIndex,
        long sourceInstanceId,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.EffectResolved,
            playerIndex,
            sourceInstanceId,
            0,
            0,
            phase);
    }

    public static GameEvent SpellActivated(
        int playerIndex,
        long instanceId,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.SpellActivated,
            playerIndex,
            instanceId,
            0,
            0,
            phase);
    }

    public static GameEvent CardSet(
        int playerIndex,
        long instanceId,
        int zoneIndex)
    {
        return new GameEvent(
            GameEventType.CardSet,
            playerIndex,
            instanceId,
            0,
            zoneIndex,
            TurnPhase.Main);
    }

    public static GameEvent TrapActivated(
        int playerIndex,
        long instanceId,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.TrapActivated,
            playerIndex,
            instanceId,
            0,
            0,
            phase);
    }

    public static GameEvent FieldSpellActivated(
        int playerIndex,
        long instanceId)
    {
        return new GameEvent(
            GameEventType.FieldSpellActivated,
            playerIndex,
            instanceId,
            0,
            0,
            TurnPhase.Main);
    }

    public static GameEvent EquipAttached(
        int playerIndex,
        long instanceId,
        long targetInstanceId)
    {
        return new GameEvent(
            GameEventType.EquipAttached,
            playerIndex,
            instanceId,
            targetInstanceId,
            0,
            TurnPhase.Main);
    }

    public static GameEvent ChainStarted(
        int playerIndex,
        long sourceInstanceId,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.ChainStarted,
            playerIndex,
            sourceInstanceId,
            0,
            1,
            phase);
    }

    public static GameEvent ChainLinkAdded(
        int playerIndex,
        long sourceInstanceId,
        int chainLinkNumber,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.ChainLinkAdded,
            playerIndex,
            sourceInstanceId,
            0,
            chainLinkNumber,
            phase);
    }

    public static GameEvent ResponsePassed(
        int playerIndex,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.ResponsePassed,
            playerIndex,
            0,
            0,
            0,
            phase);
    }

    public static GameEvent CardActivationNegated(
        int playerIndex,
        long sourceInstanceId,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.CardActivationNegated,
            playerIndex,
            sourceInstanceId,
            0,
            0,
            phase);
    }

    public static GameEvent MonsterAttackModified(
        int playerIndex,
        long instanceId,
        int amount,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.MonsterAttackModified,
            playerIndex,
            instanceId,
            0,
            amount,
            phase);
    }

    public static GameEvent MonsterDefenseModified(
        int playerIndex,
        long instanceId,
        int amount,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.MonsterDefenseModified,
            playerIndex,
            instanceId,
            0,
            amount,
            phase);
    }

    public static GameEvent MonsterHealthModified(
        int playerIndex,
        long instanceId,
        int amount,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.MonsterHealthModified,
            playerIndex,
            instanceId,
            0,
            amount,
            phase);
    }

    public static GameEvent FieldMaintenancePaid(
        int playerIndex,
        long instanceId,
        int amount)
    {
        return new GameEvent(
            GameEventType.FieldMaintenancePaid,
            playerIndex,
            instanceId,
            0,
            amount,
            TurnPhase.End);
    }

    public static GameEvent CardPlacedInSpellTrapZone(
        int playerIndex,
        long instanceId,
        int zoneIndex,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.CardPlacedInSpellTrapZone,
            playerIndex,
            instanceId,
            0,
            zoneIndex,
            phase);
    }

    public static GameEvent CardSentToGraveyard(
        int playerIndex,
        long instanceId,
        TurnPhase phase)
    {
        return new GameEvent(
            GameEventType.CardSentToGraveyard,
            playerIndex,
            instanceId,
            0,
            0,
            phase);
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
