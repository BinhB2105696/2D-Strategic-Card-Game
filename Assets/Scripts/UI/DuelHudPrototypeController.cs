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
        OpenConfirmation(
            PendingConfirmation.EnterBattle,
            "Enter Battle Phase?\nYou cannot return to Main Phase this turn.");
    }

    private void RequestEndTurn()
    {
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
        Debug.Log(
            "Prototype HUD confirmation accepted for " + pendingConfirmation +
            ". Gameplay state is intentionally unchanged until the HUD is bound " +
            "to GameAction and GameRules.",
            this);

        pendingConfirmation = PendingConfirmation.None;
        SetConfirmationVisible(false);
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
