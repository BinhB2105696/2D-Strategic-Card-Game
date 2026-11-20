using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// Runtime driver for PvE and local auto-play.
public sealed class DuelEasyBotController : MonoBehaviour
{
    private const int DefaultBotPlayerIndex = 1;
    private const float DecisionDelaySeconds = 0.45f;

    private DuelBoardReadOnlyDemo board;
    private IBot bot;
    private IBot autoPlayerBot;
    private PendingHardDecision pendingHardDecision;
    private float nextDecisionTime;
    private bool wasWaitingForBot;
    private int waitingPlayerIndex = -1;

    private sealed class PendingHardDecision
    {
        public GameSession Session;
        public int ActionCount;
        public int PlayerIndex;
        public BotGameStateView View;
        public IReadOnlyList<GameAction> LegalActions;
        public Task<GameAction> Choice;
    }

    public bool IsEnabled { get; private set; }
    public bool IsAutoPlayerEnabled { get; private set; }
    public int BotPlayerIndex { get; private set; } = DefaultBotPlayerIndex;
    public int HumanPlayerIndex => 1 - BotPlayerIndex;
    public string BotDisplayName => bot != null ? bot.DisplayName : "Easy Bot";

    public void Initialize(
        DuelBoardReadOnlyDemo duelBoard,
        IBot controlledBot,
        int botPlayerIndex = DefaultBotPlayerIndex,
        IBot autoPlayerBot = null)
    {
        board = duelBoard;
        bot = controlledBot;
        this.autoPlayerBot = autoPlayerBot;
        BotPlayerIndex = botPlayerIndex;
        IsEnabled = false;
        IsAutoPlayerEnabled = false;
        wasWaitingForBot = false;
        waitingPlayerIndex = -1;
    }

    public void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
            return;

        IsEnabled = enabled;
        if (!enabled)
            IsAutoPlayerEnabled = false;
        wasWaitingForBot = false;
        waitingPlayerIndex = -1;
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
        return (IsEnabled && playerIndex == BotPlayerIndex) ||
            (IsAutoPlayerEnabled && playerIndex == HumanPlayerIndex);
    }

    public void SetAutoPlayerEnabled(bool enabled)
    {
        if (IsAutoPlayerEnabled == enabled && (!enabled || IsEnabled))
            return;

        IsAutoPlayerEnabled = enabled;
        if (enabled)
            IsEnabled = true;

        wasWaitingForBot = false;
        waitingPlayerIndex = -1;
        nextDecisionTime = Time.unscaledTime + DecisionDelaySeconds;
        board?.CloseCardWindows();
        board?.RefreshBoard();
        Debug.Log(
            enabled
                ? BotDisplayName +
                  " now controls both players in Auto mode."
                : BotDisplayName +
                  " stopped controlling Player 1; Player 2 remains enabled.",
            this);
    }

    private void Update()
    {
        if ((!IsEnabled && !IsAutoPlayerEnabled) ||
            board == null || board.Session == null)
            return;
        if (board.IsPresenting)
        {
            wasWaitingForBot = false;
            return;
        }

        if (pendingHardDecision != null)
        {
            FinishHardDecision();
            return;
        }

        GameState state = board.Session.State;
        int controlledPlayerIndex = state.InteractionPlayerIndex;
        if (state.IsFinished || !ControlsPlayer(controlledPlayerIndex))
        {
            wasWaitingForBot = false;
            waitingPlayerIndex = -1;
            return;
        }

        if (!wasWaitingForBot ||
            waitingPlayerIndex != controlledPlayerIndex)
        {
            wasWaitingForBot = true;
            waitingPlayerIndex = controlledPlayerIndex;
            nextDecisionTime = Time.unscaledTime + DecisionDelaySeconds;
            return;
        }

        if (Time.unscaledTime < nextDecisionTime)
            return;

        TakeOneDecision(state, controlledPlayerIndex);
        nextDecisionTime = Time.unscaledTime + DecisionDelaySeconds;
    }

    private void TakeOneDecision(GameState state, int playerIndex)
    {
        IReadOnlyList<GameAction> legalActions = GameRules.GetLegalActions(
            state,
            playerIndex);
        if (legalActions.Count == 0)
        {
            Debug.LogError(
                BotDisplayName + " (Player " + (playerIndex + 1) +
                ") reached a decision state with no legal " +
                "actions: Turn " + state.TurnNumber + ", " +
                state.CurrentPhase + ".",
                this);
            DisableAllAutomation();
            board.RefreshBoard();
            return;
        }

        BotGameStateView view = BotGameStateView.Create(
            state,
            playerIndex);
        IBot decisionBot = playerIndex == BotPlayerIndex ? bot : autoPlayerBot ?? bot;
        if (decisionBot is HardBot hardBot)
        {
            // Capture on the Unity thread; the worker operates only on the isolated rules state.
            GameState snapshot = state.CreateSearchFork();
            pendingHardDecision = new PendingHardDecision
            {
                Session = board.Session,
                ActionCount = board.Session.Log.Actions.Count,
                PlayerIndex = playerIndex,
                View = view,
                LegalActions = legalActions,
                Choice = Task.Run(() =>
                {
                    hardBot.Observe(snapshot, playerIndex);
                    return hardBot.ChooseAction(view, legalActions);
                })
            };
            return;
        }
        if (decisionBot is IStateObservingBot observingBot)
            observingBot.Observe(state, playerIndex);
        ApplyDecision(playerIndex, view, legalActions, decisionBot.ChooseAction(view, legalActions));
    }

    private void FinishHardDecision()
    {
        PendingHardDecision decision = pendingHardDecision;
        if (!decision.Choice.IsCompleted)
            return;
        pendingHardDecision = null;

        var error = decision.Choice.Exception;
        if (!ReferenceEquals(board.Session, decision.Session) ||
            board.Session.Log.Actions.Count != decision.ActionCount || board.Session.State.IsFinished ||
            board.Session.State.InteractionPlayerIndex != decision.PlayerIndex || !ControlsPlayer(decision.PlayerIndex))
            return;

        if (decision.Choice.Status != TaskStatus.RanToCompletion)
        {
            Debug.LogError("Hard Bot could not finish its decision: " + error?.GetBaseException().Message, this);
            DisableAllAutomation();
            board.RefreshBoard();
            return;
        }

        ApplyDecision(decision.PlayerIndex, decision.View, decision.LegalActions, decision.Choice.Result);
        nextDecisionTime = Time.unscaledTime + DecisionDelaySeconds;
    }

    private void ApplyDecision(int playerIndex, BotGameStateView view,
        IReadOnlyList<GameAction> legalActions, GameAction action)
    {
        if (action == null ||
            !ContainsReference(legalActions, action))
        {
            Debug.LogError(
                BotDisplayName + " did not return an action from the legal " +
                "action list.",
                this);
            DisableAllAutomation();
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
            DisableAllAutomation();
            board.RefreshBoard();
            return;
        }

        Debug.Log(
            BotDisplayName + " (Player " + (playerIndex + 1) +
            ") accepted action: " +
            DescribeAction(view, action) +
            " (" + result.Events.Count + " event(s)).",
            this);

        if (action.Type == GameActionType.EndTurn)
        {
            board.PresentTurnTransitionThenResolveAutomatic();
            return;
        }

        board.RefreshBoard();
    }

    private void DisableAllAutomation()
    {
        IsEnabled = false;
        IsAutoPlayerEnabled = false;
        wasWaitingForBot = false;
        waitingPlayerIndex = -1;
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
