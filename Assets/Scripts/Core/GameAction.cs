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
    ActivateCard,
    SetTrap,
    PassResponse,
    EnterEndPhase,
    ResolveTriggeredEffect,
    DeclineTriggeredEffect
}

[Serializable]
public sealed class GameAction
{
    private readonly long[] targetInstanceIds;

    public GameActionType Type { get; }
    public int PlayerIndex { get; }
    public long SourceInstanceId { get; }
    public long TargetInstanceId => targetInstanceIds.Length > 0
        ? targetInstanceIds[0]
        : 0;
    public IReadOnlyList<long> TargetInstanceIds => targetInstanceIds;
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
        long[] tributeInstanceIds = null,
        long[] targetInstanceIds = null)
    {
        Type = type;
        PlayerIndex = playerIndex;
        SourceInstanceId = sourceInstanceId;
        this.targetInstanceIds = targetInstanceIds != null
            ? (long[])targetInstanceIds.Clone()
            : targetInstanceId == 0
                ? new long[0]
                : new[] { targetInstanceId };
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

    public static GameAction ActivateCard(
        int playerIndex,
        long cardInstanceId,
        int zoneIndex = -1,
        long targetInstanceId = 0)
    {
        return new GameAction(
            GameActionType.ActivateCard,
            playerIndex,
            cardInstanceId,
            targetInstanceId,
            zoneIndex);
    }

    public static GameAction ActivateCardWithTargets(
        int playerIndex,
        long cardInstanceId,
        int zoneIndex,
        params long[] targetInstanceIds)
    {
        return new GameAction(
            GameActionType.ActivateCard,
            playerIndex,
            cardInstanceId,
            zoneIndex: zoneIndex,
            targetInstanceIds: targetInstanceIds);
    }

    public static GameAction SetTrap(
        int playerIndex,
        long cardInstanceId,
        int zoneIndex)
    {
        return new GameAction(
            GameActionType.SetTrap,
            playerIndex,
            cardInstanceId,
            zoneIndex: zoneIndex);
    }

    public static GameAction PassResponse(int playerIndex)
    {
        return new GameAction(
            GameActionType.PassResponse,
            playerIndex);
    }

    public static GameAction ResolveTriggeredEffect(
        int playerIndex,
        long sourceInstanceId,
        long selectedInstanceId)
    {
        return new GameAction(
            GameActionType.ResolveTriggeredEffect,
            playerIndex,
            sourceInstanceId,
            selectedInstanceId);
    }

    public static GameAction DeclineTriggeredEffect(
        int playerIndex,
        long sourceInstanceId)
    {
        return new GameAction(
            GameActionType.DeclineTriggeredEffect,
            playerIndex,
            sourceInstanceId);
    }

    public override string ToString()
    {
        return $"{Type} by Player {PlayerIndex}";
    }
}
