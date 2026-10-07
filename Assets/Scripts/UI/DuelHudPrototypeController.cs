using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DuelHudPrototypeController : MonoBehaviour
{
    private enum PendingConfirmation
    {
        None,
        EnterBattle,
        EndTurn,
        Surrender
    }

    [Header("Phase Menu")]
    [SerializeField] private Button phaseMenuButton;
    [SerializeField] private GameObject phaseChoices;
    [SerializeField] private Button battlePhaseButton;
    [SerializeField] private Button endPhaseButton;

    [Header("Settings Overlay")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button closeSettingsButton;
    [SerializeField] private GameObject settingsOverlay;
    [SerializeField] private Button duelLogTabButton;
    [SerializeField] private Button deckInfoTabButton;
    [SerializeField] private Button duelSettingsTabButton;
    [SerializeField] private GameObject duelLogContent;
    [SerializeField] private GameObject deckInfoContent;
    [SerializeField] private GameObject duelSettingsContent;
    [SerializeField] private Button surrenderButton;

    [Header("Confirmation Overlay")]
    [SerializeField] private GameObject confirmationPopup;
    [SerializeField] private TMP_Text confirmationMessageText;
    [SerializeField] private Button confirmationAcceptButton;
    [SerializeField] private Button confirmationCancelButton;

    private PendingConfirmation pendingConfirmation;

    private void Awake()
    {
        BindButtons();
        SetPhaseChoicesVisible(false);
        SetSettingsVisible(false);
        SetConfirmationVisible(false);
        ShowDuelLogTab();
    }

    public void Configure(
        Button newPhaseMenuButton,
        GameObject newPhaseChoices,
        Button newBattlePhaseButton,
        Button newEndPhaseButton,
        Button newSettingsButton,
        Button newCloseSettingsButton,
        GameObject newSettingsOverlay,
        Button newDuelLogTabButton,
        Button newDeckInfoTabButton,
        Button newDuelSettingsTabButton,
        GameObject newDuelLogContent,
        GameObject newDeckInfoContent,
        GameObject newDuelSettingsContent,
        Button newSurrenderButton,
        GameObject newConfirmationPopup,
        TMP_Text newConfirmationMessageText,
        Button newConfirmationAcceptButton,
        Button newConfirmationCancelButton)
    {
        phaseMenuButton = newPhaseMenuButton;
        phaseChoices = newPhaseChoices;
        battlePhaseButton = newBattlePhaseButton;
        endPhaseButton = newEndPhaseButton;
        settingsButton = newSettingsButton;
        closeSettingsButton = newCloseSettingsButton;
        settingsOverlay = newSettingsOverlay;
        duelLogTabButton = newDuelLogTabButton;
        deckInfoTabButton = newDeckInfoTabButton;
        duelSettingsTabButton = newDuelSettingsTabButton;
        duelLogContent = newDuelLogContent;
        deckInfoContent = newDeckInfoContent;
        duelSettingsContent = newDuelSettingsContent;
        surrenderButton = newSurrenderButton;
        confirmationPopup = newConfirmationPopup;
        confirmationMessageText = newConfirmationMessageText;
        confirmationAcceptButton = newConfirmationAcceptButton;
        confirmationCancelButton = newConfirmationCancelButton;
    }

    private void BindButtons()
    {
        AddListener(phaseMenuButton, TogglePhaseChoices);
        AddListener(battlePhaseButton, RequestBattlePhase);
        AddListener(endPhaseButton, RequestEndTurn);
        AddListener(settingsButton, OpenSettings);
        AddListener(closeSettingsButton, CloseSettings);
        AddListener(duelLogTabButton, ShowDuelLogTab);
        AddListener(deckInfoTabButton, ShowDeckInfoTab);
        AddListener(duelSettingsTabButton, ShowDuelSettingsTab);
        AddListener(surrenderButton, RequestSurrender);
        AddListener(confirmationAcceptButton, AcceptPrototypeConfirmation);
        AddListener(confirmationCancelButton, CancelConfirmation);
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private void TogglePhaseChoices()
    {
        bool becomesVisible = phaseChoices != null && !phaseChoices.activeSelf;
        SetPhaseChoicesVisible(becomesVisible);
    }

    private void RequestBattlePhase()
    {
        if (!TryGetDuel(out _, out GameState state) ||
            state.CurrentPhase != TurnPhase.Main ||
            state.TurnNumber == 1 ||
            state.Players[state.CurrentPlayerIndex]
                .IsBattlePhaseSkipScheduled(state.TurnNumber))
            return;

        OpenConfirmation(
            PendingConfirmation.EnterBattle,
            "Enter Battle Phase?\nYou cannot return to Main Phase this turn.");
    }

    private void RequestEndTurn()
    {
        if (!TryGetDuel(out _, out GameState state) ||
            (state.CurrentPhase != TurnPhase.Main &&
             state.CurrentPhase != TurnPhase.Battle &&
             state.CurrentPhase != TurnPhase.End))
            return;

        OpenConfirmation(
            PendingConfirmation.EndTurn,
            "End the current turn?");
    }

    private void RequestSurrender()
    {
        SetSettingsVisible(false);
        OpenConfirmation(
            PendingConfirmation.Surrender,
            "Surrender this duel?\nThis action will count as a loss.");
    }

    private void OpenSettings()
    {
        SetPhaseChoicesVisible(false);
        SetSettingsVisible(true);
        ShowDuelLogTab();
    }

    private void CloseSettings()
    {
        SetSettingsVisible(false);
    }

    private void OpenConfirmation(PendingConfirmation action, string message)
    {
        pendingConfirmation = action;

        if (confirmationMessageText != null)
        {
            confirmationMessageText.text = message;
        }

        SetPhaseChoicesVisible(false);
        SetConfirmationVisible(true);
    }

    private void AcceptPrototypeConfirmation()
    {
        PendingConfirmation confirmedAction = pendingConfirmation;
        pendingConfirmation = PendingConfirmation.None;
        SetConfirmationVisible(false);

        if (confirmedAction == PendingConfirmation.Surrender)
        {
            Debug.Log("Surrender remains a prototype action.", this);
            return;
        }

        if (!TryGetDuel(out DuelBoardReadOnlyDemo board, out GameState state))
            return;

        int playerIndex = state.CurrentPlayerIndex;
        if (confirmedAction == PendingConfirmation.EnterBattle)
        {
            if (state.CurrentPhase != TurnPhase.Main ||
                state.TurnNumber == 1 ||
                state.Players[playerIndex]
                    .IsBattlePhaseSkipScheduled(state.TurnNumber))
                return;

            ApplyAction(board, GameAction.AdvancePhase(playerIndex));
        }
        else if (confirmedAction == PendingConfirmation.EndTurn)
        {
            if (state.CurrentPhase == TurnPhase.Main ||
                state.CurrentPhase == TurnPhase.Battle)
            {
                if (!ApplyAction(
                    board, GameAction.EnterEndPhase(playerIndex), false))
                    return;
            }

            if (!ApplyAction(board, GameAction.EndTurn(playerIndex), false))
                return;

            if (!board.ResolveAutomaticTurnStart())
            {
                board.RefreshBoard();
                Debug.LogError("Could not complete the next turn's automatic steps.", this);
            }
        }
    }

    private bool TryGetDuel(
        out DuelBoardReadOnlyDemo board,
        out GameState state)
    {
        board = GetComponentInParent<DuelBoardReadOnlyDemo>();
        state = board != null && board.Session != null
            ? board.Session.State
            : null;
        return state != null && !state.IsFinished;
    }

    private bool ApplyAction(
        DuelBoardReadOnlyDemo board,
        GameAction action,
        bool refreshBoard = true)
    {
        ActionResult result = board.TryApplyAction(action, refreshBoard);
        if (result.Succeeded)
            return true;

        board.RefreshBoard();
        Debug.LogWarning("Phase action rejected: " + result.ErrorMessage, this);
        return false;
    }

    private void CancelConfirmation()
    {
        pendingConfirmation = PendingConfirmation.None;
        SetConfirmationVisible(false);
    }

    private void ShowDuelLogTab()
    {
        SetTab(duelLogContent);
    }

    private void ShowDeckInfoTab()
    {
        SetTab(deckInfoContent);
    }

    private void ShowDuelSettingsTab()
    {
        SetTab(duelSettingsContent);
    }

    private void SetTab(GameObject visibleContent)
    {
        if (duelLogContent != null)
        {
            duelLogContent.SetActive(visibleContent == duelLogContent);
        }

        if (deckInfoContent != null)
        {
            deckInfoContent.SetActive(visibleContent == deckInfoContent);
        }

        if (duelSettingsContent != null)
        {
            duelSettingsContent.SetActive(visibleContent == duelSettingsContent);
        }
    }

    private void SetPhaseChoicesVisible(bool visible)
    {
        if (phaseChoices != null)
        {
            phaseChoices.SetActive(visible);
        }
    }

    private void SetSettingsVisible(bool visible)
    {
        if (settingsOverlay != null)
        {
            settingsOverlay.SetActive(visible);
        }
    }

    private void SetConfirmationVisible(bool visible)
    {
        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(visible);
        }
    }
}
