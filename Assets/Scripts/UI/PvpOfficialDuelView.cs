using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Perspective-only presentation of the real PvE scene. No GameSession is created on the client.
public sealed partial class PvpOfficialDuelView : MonoBehaviour
{
    private DuelBoardReadOnlyDemo assets;
    private DuelCardInteractionController windows;
    private DuelPresentationController motion;
    private PvpDuelCoordinator coordinator;
    private PvpRoomConnection room;
    private PvpHudBindings hud;
    private AppDuelFlow result;
    private IReadOnlyDictionary<string, CardData> catalog;
    private Dictionary<string, CardDefinition> definitions;
    private LocalPlayerProfile profile;
    private Transform boardLayer, hudLayer, overlay;
    private PvpMatchView displayed, queued;
    private bool busy, initialized, boardReadySent;
    private int lastRevision = -1, lastSequence;
    private string savedMatch;
    private Action exit;
    private Sprite[] avatars;

    public void Configure(DuelBoardReadOnlyDemo board, PvpDuelCoordinator duel, PvpRoomConnection connection,
        IReadOnlyDictionary<string, CardData> cards, LocalPlayerProfile localProfile, Sprite avatar, Sprite[] avatarChoices,
        GameObject resultPrefab, Action returnHome)
    {
        assets = board; coordinator = duel; room = connection; catalog = cards; profile = localProfile; exit = returnHome;
        avatars = avatarChoices;
        definitions = cards.ToDictionary(item => item.Key, item => CardDataConverter.ToDefinition(item.Value));
        boardLayer = transform.Find("SafeAreaRoot/BoardLayer");
        hudLayer = transform.Find("SafeAreaRoot/HudLayer");
        overlay = transform.Find("SafeAreaRoot/OverlayLayer");
        if (boardLayer == null || hudLayer == null || overlay == null || resultPrefab == null)
            throw new InvalidOperationException("The authored duel board/HUD/overlay is missing.");
        assets.InitializeNetworkAssets(cards, avatar);
        windows = GetComponent<DuelCardInteractionController>() ?? gameObject.AddComponent<DuelCardInteractionController>();
        windows.enabled = false; windows.Initialize(board);
        windows.NetworkClosed = () => { workflow = false; requestedKey = ""; ClearHighlights(); };
        motion = GetComponent<DuelPresentationController>() ?? gameObject.AddComponent<DuelPresentationController>();
        motion.Initialize(board); motion.enabled = false;
        hud = GetComponentInChildren<DuelHudPrototypeController>(true).BindNetworkView();
        BindControls();
        GameObject resultObject = Instantiate(resultPrefab);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(resultObject, gameObject.scene);
        result = resultObject.GetComponent<AppDuelFlow>(); result.InitializeNetwork(() => exit());
        DuelLogView log = hud.contents[0].GetComponent<DuelLogView>() ?? hud.contents[0].AddComponent<DuelLogView>();
        log.InitializeNetwork(() => coordinator.Journal, () => coordinator.View?.logSequence ?? 0);
        SetInformation(hud.contents[1], "Your saved deck\n" + profile.decks[profile.selectedDeck].name + "\n\n" +
            string.Join("\n", profile.decks[profile.selectedDeck].cardIds.GroupBy(id => id)
                .Select(group => cards[group.Key].CardName + " x" + group.Count())));
        SetInformation(hud.contents[2], "PvP\n120 seconds per turn.\nLeaving the match is a defeat.");
        initialized = true;
    }

    public void Receive(PvpMatchView view)
    {
        if (!initialized || view == null || !view.started) return;
        if (busy && view.finished && view.reason != "Rules")
        {
            StopAllCoroutines();
            foreach (Transform child in overlay) if (child.name == "PvpAcceptedMotion") Destroy(child.gameObject);
            queued = null; busy = false; workflow = false; ClearHighlights();
            displayed = view; Render(view); lastRevision = view.revision; RefreshControls(); return;
        }
        if (busy) { queued = view; return; }
        if (view.revision != lastRevision) StartCoroutine(Present(view));
        else { displayed = view; ProcessChoices(view); RefreshControls(); }
    }

    private void Update()
    {
        if (!initialized || coordinator.View == null) return;
        float remaining = coordinator.RemainingSeconds;
        int value = Mathf.CeilToInt(remaining);
        hud.timer.text = (coordinator.View.timerActor == coordinator.View.seat ? "YOU\n" : "OPP\n") +
            (value / 60).ToString("00") + ":" + (value % 60).ToString("00");
        hud.timer.color = remaining <= 15 ? new Color32(255, 100, 80, 255) :
            coordinator.View.timerActor == coordinator.View.seat ? new Color32(255, 224, 100, 255) : Color.white;
        hud.timer.gameObject.SetActive(true);
        if (Input.GetKeyDown(KeyCode.Escape) && !coordinator.View.finished && !busy && !coordinator.Pending)
        {
            if (hud.confirmation.activeSelf) hud.confirmation.SetActive(false);
            else if (hud.settingsPanel.activeSelf) hud.settingsPanel.SetActive(false);
            else if (windows.HasNetworkWindow)
            { windows.CloseAllCardWindows(); workflow = false; ClearHighlights(); }
            else Confirm("Leave this match and lose?", () => coordinator.Surrender());
        }
    }

    private void BindControls()
    {
        hud.phase.onClick.AddListener(() => hud.phases.SetActive(!hud.phases.activeSelf));
        hud.battle.onClick.AddListener(() => Confirm("Enter Battle Phase?", () => Begin(0, "AdvancePhase", true)));
        hud.end.onClick.AddListener(() => Confirm("End your turn?", () =>
            Begin(0, displayed.phase == "End" ? "EndTurn" : "EnterEndPhase", true)));
        hud.settings.onClick.AddListener(() => { windows.CloseAllCardWindows(); hud.settingsPanel.SetActive(true); ShowTab(0); });
        hud.closeSettings.onClick.AddListener(() => hud.settingsPanel.SetActive(false));
        hud.surrender.onClick.AddListener(() => Confirm("Surrender this match?", () => coordinator.Surrender()));
        hud.cancel.onClick.AddListener(() => hud.confirmation.SetActive(false));
        for (int index = 0; index < hud.tabs.Length; index++)
        { int tab = index; hud.tabs[index].onClick.AddListener(() => ShowTab(tab)); }
        BindPile("Player", true); BindPile("Opp", false);
    }

    private void Confirm(string title, Action action)
    {
        hud.phases.SetActive(false); hud.confirmationText.text = title;
        hud.confirmation.SetActive(true); hud.accept.onClick.RemoveAllListeners();
        hud.accept.onClick.AddListener(() => { hud.confirmation.SetActive(false); action(); });
    }

    private void ShowTab(int tab)
    {
        for (int index = 0; index < hud.contents.Length; index++) hud.contents[index].SetActive(index == tab);
    }

    private void RefreshControls()
    {
        PvpMatchView view = coordinator.View;
        if (view == null) return;
        bool act = CanAct;
        hud.phase.interactable = act && !view.response && !view.trigger;
        hud.battle.interactable = act && view.phase == "Main" && view.choices.transitions.Contains("AdvancePhase");
        hud.end.interactable = act && (view.choices.transitions.Contains("EnterEndPhase") || view.choices.transitions.Contains("EndTurn"));
        hud.surrender.interactable = !view.finished && room.Connected;
        hud.settings.interactable = !busy && !coordinator.Pending;
        hud.accept.interactable = !busy && !coordinator.Pending && room.Connected && !view.finished;
        if (view.finished)
        {
            windows.CloseAllCardWindows(); hud.settingsPanel.SetActive(false); hud.confirmation.SetActive(false);
            string warning = null;
            if (savedMatch != view.matchId)
            { if (PvpLocalHistory.Save(view, out warning)) savedMatch = view.matchId; }
            if (!coordinator.CanLeaveResult) warning = "Waiting for opponent to acknowledge the result.";
            result.ShowNetworkResult(view, warning);
        }
    }

    private bool CanAct => initialized && !busy && coordinator.View != null &&
        coordinator.View.clockStarted && !coordinator.View.finished && !coordinator.Pending && room.Connected &&
        coordinator.View.interactionPlayer == coordinator.View.seat;

    public void ShowSaveError(string message) { result.ShowNetworkResult(coordinator.View, message); }

#if UNITY_EDITOR
    public void VerifyPreviewLayout(PvpMatchView view)
    {
        displayed = view; Render(view);
    }
#endif

    private void SetInformation(GameObject target, string message)
    {
        TMP_Text text = target.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(item => item.GetComponentInParent<Button>() == null);
        if (text == null)
        {
            var go = new GameObject("NetworkInformation", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(target.transform, false); text = go.GetComponent<TMP_Text>();
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(25, 150); text.rectTransform.offsetMax = new Vector2(-25, -20);
        }
        text.font = assets.NetworkFont; text.fontSize = 27; text.richText = false;
        text.enableWordWrapping = true; text.text = message;
    }
}
