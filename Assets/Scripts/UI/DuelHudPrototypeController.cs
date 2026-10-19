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
    private DuelEasyBotController easyBotController;
    private Button easyBotToggleButton;
    private TMP_Text easyBotToggleText;
    private Button autoPlayButton;
    private TMP_Text autoPlayText;

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

    public void ConfigureEasyBot(DuelEasyBotController controller)
    {
        easyBotController = controller;
        if (easyBotToggleButton == null)
            CreateEasyBotToggle();
        if (autoPlayButton == null)
            CreateAutoPlayToggle();
        RefreshEasyBotToggle();
        RefreshAutoPlayToggle();
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
        if (TryGetDuel(out _, out GameState state))
            RefreshPhaseControls(state);

        bool becomesVisible = phaseChoices != null && !phaseChoices.activeSelf;
        if (becomesVisible)
        {
            CloseCardWindows();
            SetSettingsVisible(false);
        }
        SetPhaseChoicesVisible(becomesVisible);
    }

    public void PrepareForCardSelection()
    {
        SetPhaseChoicesVisible(false);
        SetSettingsVisible(false);
    }

    public void RefreshPhaseControls(GameState state)
    {
        DuelBoardReadOnlyDemo board =
            GetComponentInParent<DuelBoardReadOnlyDemo>();
        bool humanMayUsePhaseControls = board == null ||
            !board.IsAutomatedPlayer(state.InteractionPlayerIndex);
        GameAction enterBattle = FindLegalAction(
            state,
            GameActionType.AdvancePhase);
        GameAction enterEnd = FindLegalAction(
            state,
            GameActionType.EnterEndPhase);
        GameAction endTurn = FindLegalAction(
            state,
            GameActionType.EndTurn);
        bool canEnterBattle = enterBattle != null &&
            state.CurrentPhase == TurnPhase.Main;
        bool canEndTurn = enterEnd != null || endTurn != null;
        bool mayOpenPhaseMenu = humanMayUsePhaseControls &&
            (canEnterBattle || canEndTurn);

        if (phaseMenuButton != null)
            phaseMenuButton.interactable = mayOpenPhaseMenu;

        if (battlePhaseButton != null)
            battlePhaseButton.interactable =
                humanMayUsePhaseControls && canEnterBattle;

        if (endPhaseButton != null)
            endPhaseButton.interactable =
                humanMayUsePhaseControls && canEndTurn;

        if (!mayOpenPhaseMenu)
            SetPhaseChoicesVisible(false);

        if (autoPlayButton != null)
            autoPlayButton.interactable = !state.IsFinished;
        RefreshAutoPlayToggle();
    }

    private void RequestBattlePhase()
    {
        if (!TryGetDuel(out DuelBoardReadOnlyDemo board, out GameState state) ||
            board.IsAutomatedPlayer(state.InteractionPlayerIndex) ||
            FindLegalAction(state, GameActionType.AdvancePhase) == null)
            return;

        OpenConfirmation(
            PendingConfirmation.EnterBattle,
            "Enter Battle Phase?\nYou cannot return to Main Phase this turn.");
    }

    private void RequestEndTurn()
    {
        if (!TryGetDuel(out DuelBoardReadOnlyDemo board, out GameState state) ||
            board.IsAutomatedPlayer(state.InteractionPlayerIndex) ||
            (FindLegalAction(state, GameActionType.EnterEndPhase) == null &&
             FindLegalAction(state, GameActionType.EndTurn) == null))
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
        CloseCardWindows();
        SetPhaseChoicesVisible(false);
        SetSettingsVisible(true);
        ShowDuelLogTab();
    }

    private void CloseSettings()
    {
        SetSettingsVisible(false);
        if (TryGetDuel(out DuelBoardReadOnlyDemo board, out GameState state) &&
            state.HasOpenResponseWindow)
        {
            board.RefreshBoard();
        }
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

        if (confirmedAction == PendingConfirmation.EnterBattle)
        {
            GameAction enterBattle = FindLegalAction(
                state,
                GameActionType.AdvancePhase);
            if (enterBattle == null)
                return;

            ApplyAction(board, enterBattle);
        }
        else if (confirmedAction == PendingConfirmation.EndTurn)
        {
            GameAction enterEnd = FindLegalAction(
                state,
                GameActionType.EnterEndPhase);
            if (enterEnd != null)
            {
                if (!ApplyAction(board, enterEnd, false))
                    return;
            }

            GameAction endTurn = FindLegalAction(
                state,
                GameActionType.EndTurn);
            if (endTurn == null || !ApplyAction(board, endTurn, false))
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

    private static GameAction FindLegalAction(
        GameState state,
        GameActionType type)
    {
        if (state == null || state.IsFinished)
            return null;

        var actions = GameRules.GetLegalActions(
            state,
            state.InteractionPlayerIndex);
        for (int index = 0; index < actions.Count; index++)
        {
            if (actions[index].Type == type)
                return actions[index];
        }
        return null;
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

    private void CloseCardWindows()
    {
        DuelBoardReadOnlyDemo board =
            GetComponentInParent<DuelBoardReadOnlyDemo>();
        DuelCardInteractionController cardInteraction = board != null
            ? board.GetComponent<DuelCardInteractionController>()
            : null;
        cardInteraction?.CloseAllCardWindows();
    }

    private void CreateEasyBotToggle()
    {
        if (duelSettingsContent == null || surrenderButton == null)
            return;

        GameObject toggleObject = Instantiate(
            surrenderButton.gameObject,
            duelSettingsContent.transform);
        toggleObject.name = "EasyBotToggleButton_Runtime";
        easyBotToggleButton = toggleObject.GetComponent<Button>();
        easyBotToggleButton.onClick.RemoveAllListeners();
        easyBotToggleButton.onClick.AddListener(ToggleEasyBot);

        RectTransform toggleRect =
            toggleObject.GetComponent<RectTransform>();
        toggleRect.anchoredPosition = new Vector2(0f, 120f);

        Image background = toggleObject.GetComponent<Image>();
        if (background != null)
            background.color = new Color32(22, 77, 139, 245);

        easyBotToggleText =
            toggleObject.GetComponentInChildren<TMP_Text>(true);
    }

    private void ToggleEasyBot()
    {
        if (easyBotController == null)
            return;

        easyBotController.SetEnabled(!easyBotController.IsEnabled);
        RefreshEasyBotToggle();
        RefreshAutoPlayToggle();
        SetSettingsVisible(false);
    }

    private void RefreshEasyBotToggle()
    {
        if (easyBotToggleText == null)
            return;

        easyBotToggleText.text = easyBotController != null &&
            easyBotController.IsEnabled
            ? "Easy Bot (Player 2): ON"
            : "Easy Bot (Player 2): OFF";
    }

    private void CreateAutoPlayToggle()
    {
        Transform hudLayer = transform.parent;
        RectTransform turnPhase = hudLayer != null
            ? hudLayer.Find("TurnPhaseHUD") as RectTransform
            : null;
        if (hudLayer == null || turnPhase == null)
            return;

        GameObject buttonObject = new GameObject(
            "AutoPlayButton_Runtime",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(Canvas),
            typeof(GraphicRaycaster));
        buttonObject.layer = hudLayer.gameObject.layer;
        buttonObject.transform.SetParent(hudLayer, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(96f, 96f);
        rect.anchoredPosition = turnPhase.anchoredPosition +
            new Vector2(0f, -turnPhase.rect.height - 12f);

        Image background = buttonObject.GetComponent<Image>();
        background.color = new Color32(20, 72, 126, 240);

        autoPlayButton = buttonObject.GetComponent<Button>();
        autoPlayButton.targetGraphic = background;
        autoPlayButton.onClick.AddListener(ToggleAutoPlay);

        Canvas canvas = buttonObject.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 20;

        GameObject textObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = buttonObject.layer;
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        autoPlayText = textObject.GetComponent<TextMeshProUGUI>();
        TMP_Text phaseLabel = phaseMenuButton != null
            ? phaseMenuButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        if (phaseLabel != null)
            autoPlayText.font = phaseLabel.font;
        autoPlayText.text = "Auto";
        autoPlayText.fontSize = 28f;
        autoPlayText.fontStyle = FontStyles.Bold;
        autoPlayText.alignment = TextAlignmentOptions.Center;
        autoPlayText.color = Color.white;
        autoPlayText.raycastTarget = false;
    }

    private void ToggleAutoPlay()
    {
        if (easyBotController == null)
            return;

        easyBotController.SetAutoPlayerEnabled(
            !easyBotController.IsAutoPlayerEnabled);
        RefreshEasyBotToggle();
        RefreshAutoPlayToggle();
        SetPhaseChoicesVisible(false);
        SetSettingsVisible(false);
        CloseCardWindows();
    }

    private void RefreshAutoPlayToggle()
    {
        if (autoPlayButton == null)
            return;

        bool active = easyBotController != null &&
            easyBotController.IsAutoPlayerEnabled;
        Image background = autoPlayButton.targetGraphic as Image;
        if (background != null)
        {
            background.color = active
                ? new Color32(220, 157, 33, 250)
                : new Color32(20, 72, 126, 240);
        }
        if (autoPlayText != null)
            autoPlayText.text = "Auto";
    }
}
