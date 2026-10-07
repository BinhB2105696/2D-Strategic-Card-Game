using System;
using System.Collections.Generic;

public enum GameActionType
{
    ResolveDrawStep,
    ResolveEnergyRefreshStep,
    AdvancePhase,
    EndTurn,
    SummonMonster,
    AllocateEnergy,
    ChangeBattlePosition,
    Attack,
    ActivateNormalSpell,
    EnterEndPhase
}

[Serializable]
public sealed class GameAction
{
    public GameActionType Type { get; }
    public int PlayerIndex { get; }
    public long SourceInstanceId { get; }
    public long TargetInstanceId { get; }
    public int ZoneIndex { get; }
    public int Amount { get; }
    public BattlePosition BattlePosition { get; }
    public IReadOnlyList<long> TributeInstanceIds { get; }

    private GameAction(
        GameActionType type,
        int playerIndex,
        long sourceInstanceId = 0,
        long targetInstanceId = 0,
        int zoneIndex = -1,
        int amount = 0,
        BattlePosition battlePosition = BattlePosition.None,
        long[] tributeInstanceIds = null)
    {
        Type = type;
        PlayerIndex = playerIndex;
        SourceInstanceId = sourceInstanceId;
        TargetInstanceId = targetInstanceId;
        ZoneIndex = zoneIndex;
        Amount = amount;
        BattlePosition = battlePosition;
        TributeInstanceIds = tributeInstanceIds ?? new long[0];
    }

    public static GameAction ResolveDrawStep(int playerIndex)
    {
        return new GameAction(
            GameActionType.ResolveDrawStep,
            playerIndex);
    }

    public static GameAction ResolveEnergyRefreshStep(int playerIndex)
    {
        return new GameAction(
            GameActionType.ResolveEnergyRefreshStep,
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

    public static GameAction EnterEndPhase(int playerIndex)
    {
        return new GameAction(GameActionType.EnterEndPhase, playerIndex);
    }

    public static GameAction SummonMonster(
        int playerIndex,
        long monsterInstanceId,
        int zoneIndex,
        BattlePosition position,
        params long[] tributeInstanceIds)
    {
        long[] copiedTributes = tributeInstanceIds == null
            ? new long[0]
            : (long[])tributeInstanceIds.Clone();

        return new GameAction(
            GameActionType.SummonMonster,
            playerIndex,
            monsterInstanceId,
            zoneIndex: zoneIndex,
            battlePosition: position,
            tributeInstanceIds: copiedTributes);
    }

    public static GameAction AllocateEnergy(
        int playerIndex,
        long monsterInstanceId,
        int amount)
    {
        return new GameAction(
            GameActionType.AllocateEnergy,
            playerIndex,
            monsterInstanceId,
            amount: amount);
    }

    public static GameAction ChangeBattlePosition(
        int playerIndex,
        long monsterInstanceId,
        BattlePosition position)
    {
        return new GameAction(
            GameActionType.ChangeBattlePosition,
            playerIndex,
            monsterInstanceId,
            battlePosition: position);
    }

    public static GameAction Attack(
        int playerIndex,
        long attackerInstanceId,
        long targetInstanceId = 0)
    {
        return new GameAction(
            GameActionType.Attack,
            playerIndex,
            attackerInstanceId,
            targetInstanceId);
    }

    public static GameAction ActivateNormalSpell(
        int playerIndex,
        long spellInstanceId,
        int zoneIndex,
        long targetInstanceId = 0)
    {
        return new GameAction(
            GameActionType.ActivateNormalSpell,
            playerIndex,
            spellInstanceId,
            targetInstanceId,
            zoneIndex);
    }

    public override string ToString()
    {
        return $"{Type} by Player {PlayerIndex}";
    }
}
