using System.Collections.Generic;

public static class GameRules
{
    public static ActionResult ApplyAction(
        GameState state,
        GameAction action)
    {
        string validationError = ValidateAction(state, action);

        if (!string.IsNullOrEmpty(validationError))
        {
            return ActionResult.Failure(validationError);
        }

        switch (action.Type)
        {
            case GameActionType.ResolveDrawAndEnergy:
                return ResolveDrawAndEnergy(state);

            case GameActionType.AdvancePhase:
                return AdvancePhase(state);

            case GameActionType.EndTurn:
                return EndTurn(state);

            default:
                return ActionResult.Failure("Unknown game action type.");
        }
    }

    public static string ValidateAction(
        GameState state,
        GameAction action)
    {
        if (state == null)
        {
            return "GameState is required.";
        }

        if (action == null)
        {
            return "GameAction is required.";
        }

        if (action.PlayerIndex != state.CurrentPlayerIndex)
        {
            return "Only the current player may perform this action.";
        }

        switch (action.Type)
        {
            case GameActionType.ResolveDrawAndEnergy:
                return state.CurrentPhase == TurnPhase.DrawAndEnergy
                    ? string.Empty
                    : "Draw & Energy can only resolve in Draw & Energy Phase.";

            case GameActionType.AdvancePhase:
                return state.CurrentPhase == TurnPhase.Main ||
                       state.CurrentPhase == TurnPhase.Battle
                    ? string.Empty
                    : "Advance Phase is only valid in Main or Battle Phase.";

            case GameActionType.EndTurn:
                return state.CurrentPhase == TurnPhase.End
                    ? string.Empty
                    : "End Turn is only valid in End Phase.";

            default:
                return "Unknown game action type.";
        }
    }

    private static ActionResult ResolveDrawAndEnergy(GameState state)
    {
        PlayerState player = state.Players[state.CurrentPlayerIndex];
        int previousHandCount = player.Hand.Count;
        player.DrawCards(state.Config.CardsDrawnPerTurn);
        player.ResetAvailableEnergy();

        List<GameEvent> events = new List<GameEvent>();

        for (int index = previousHandCount; index < player.Hand.Count; index++)
        {
            events.Add(GameEvent.CardDrawn(
                player.PlayerIndex,
                player.Hand[index].InstanceId));
        }

        events.Add(GameEvent.EnergyReset(
            player.PlayerIndex,
            player.AvailableEnergy));

        state.SetCurrentPhase(TurnPhase.Main);
        events.Add(GameEvent.PhaseChanged(
            player.PlayerIndex,
            TurnPhase.Main));
        return ActionResult.Success(events);
    }

    private static ActionResult AdvancePhase(GameState state)
    {
        TurnPhase nextPhase = state.CurrentPhase == TurnPhase.Main
            ? TurnPhase.Battle
            : TurnPhase.End;

        state.SetCurrentPhase(nextPhase);
        return ActionResult.Success(new[]
        {
            GameEvent.PhaseChanged(state.CurrentPlayerIndex, nextPhase)
        });
    }

    private static ActionResult EndTurn(GameState state)
    {
        state.StartNextTurn();
        return ActionResult.Success(new[]
        {
            GameEvent.TurnStarted(
                state.CurrentPlayerIndex,
                state.TurnNumber),
            GameEvent.PhaseChanged(
                state.CurrentPlayerIndex,
                TurnPhase.DrawAndEnergy)
        });
    }
}
