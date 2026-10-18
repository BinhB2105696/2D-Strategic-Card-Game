using System.Collections.Generic;
using UnityEngine;

// Runtime driver for Human (Player 1/index 0) versus Easy Bot
// (Player 2/index 1). It never edits the saved scene.
public sealed class DuelEasyBotController : MonoBehaviour
{
    private const int DefaultBotPlayerIndex = 1;
    private const float DecisionDelaySeconds = 0.45f;

    private DuelBoardReadOnlyDemo board;
    private IBot bot;
    private float nextDecisionTime;
    private bool wasWaitingForBot;

    public bool IsEnabled { get; private set; }
    public int BotPlayerIndex { get; private set; } = DefaultBotPlayerIndex;
    public int HumanPlayerIndex => 1 - BotPlayerIndex;
    public string BotDisplayName => bot != null ? bot.DisplayName : "Easy Bot";

    public void Initialize(
        DuelBoardReadOnlyDemo duelBoard,
        IBot controlledBot,
        int botPlayerIndex = DefaultBotPlayerIndex)
    {
        board = duelBoard;
        bot = controlledBot;
        BotPlayerIndex = botPlayerIndex;
        IsEnabled = false;
        wasWaitingForBot = false;
    }

    public void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
            return;

        IsEnabled = enabled;
        wasWaitingForBot = false;
        nextDecisionTime = Time.unscaledTime + DecisionDelaySeconds;
        board?.CloseCardWindows();
        board?.RefreshBoard();
        Debug.Log(
            BotDisplayName + (enabled
                ? " now controls Player 2."
                : " disabled; local hot-seat control restored."),
            this);
    }

    public bool ControlsPlayer(int playerIndex)
    {
        return IsEnabled && playerIndex == BotPlayerIndex;
    }

    private void Update()
    {
        if (!IsEnabled || board == null || board.Session == null)
            return;

        GameState state = board.Session.State;
        if (state.IsFinished ||
            state.InteractionPlayerIndex != BotPlayerIndex)
        {
            wasWaitingForBot = false;
            return;
        }

        if (!wasWaitingForBot)
        {
            wasWaitingForBot = true;
            nextDecisionTime = Time.unscaledTime + DecisionDelaySeconds;
            return;
        }

        if (Time.unscaledTime < nextDecisionTime)
            return;

        TakeOneDecision(state);
        nextDecisionTime = Time.unscaledTime + DecisionDelaySeconds;
    }

    private void TakeOneDecision(GameState state)
    {
        IReadOnlyList<GameAction> legalActions = GameRules.GetLegalActions(
            state,
            BotPlayerIndex);
        if (legalActions.Count == 0)
        {
            Debug.LogError(
                BotDisplayName + " reached a decision state with no legal " +
                "actions: Turn " + state.TurnNumber + ", " +
                state.CurrentPhase + ".",
                this);
            IsEnabled = false;
            board.RefreshBoard();
            return;
        }

        BotGameStateView view = BotGameStateView.Create(
            state,
            BotPlayerIndex);
        GameAction action = bot.ChooseAction(view, legalActions);
        if (action == null ||
            !ContainsReference(legalActions, action))
        {
            Debug.LogError(
                BotDisplayName + " did not return an action from the legal " +
                "action list.",
                this);
            IsEnabled = false;
            board.RefreshBoard();
            return;
        }

        ActionResult result = board.TryApplyAction(action, false);
        if (!result.Succeeded)
        {
            Debug.LogError(
                BotDisplayName + " action was rejected: " +
                result.ErrorMessage,
                this);
            IsEnabled = false;
            board.RefreshBoard();
            return;
        }

        Debug.Log(
            BotDisplayName + " accepted action: " +
            DescribeAction(view, action) +
            " (" + result.Events.Count + " event(s)).",
            this);

        if (action.Type == GameActionType.EndTurn)
        {
            if (!board.ResolveAutomaticTurnStart())
            {
                Debug.LogError(
                    BotDisplayName + " could not complete automatic turn " +
                    "start steps.",
                    this);
            }
            return;
        }

        board.RefreshBoard();
    }

    private static bool ContainsReference(
        IReadOnlyList<GameAction> actions,
        GameAction selected)
    {
        for (int index = 0; index < actions.Count; index++)
        {
            if (ReferenceEquals(actions[index], selected))
                return true;
        }
        return false;
    }

    private static string DescribeAction(
        BotGameStateView view,
        GameAction action)
    {
        if (action.SourceInstanceId == 0)
            return action.Type.ToString();

        BotCardView source = view.FindCard(action.SourceInstanceId);
        string sourceName = source != null
            ? source.CardName + " #" + source.InstanceId
            : "source #" + action.SourceInstanceId;

        if (action.Type == GameActionType.Attack)
        {
            if (action.TargetInstanceId == 0)
                return sourceName + " declared a direct attack";

            BotCardView target = view.FindCard(action.TargetInstanceId);
            string targetName = target != null
                ? target.CardName + " #" + target.InstanceId
                : "target #" + action.TargetInstanceId;
            return sourceName + " attacked " + targetName;
        }

        return action.Type + " with " + sourceName;
    }
}
