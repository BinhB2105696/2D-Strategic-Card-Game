using TMPro;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Presentation/account adapter only; no duel rules are changed here.
public sealed class AppDuelFlow : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text description;
    [SerializeField] private Button home;
    [SerializeField] private Button cancel;
    private DuelBoardReadOnlyDemo board;
    private DuelEasyBotController bot;
    private AppMatchRequest request;
    private CardData[] opponentDeck;
    private bool resultShown, saved, usedAuto, pendingExit;
    private string outcome;

    public void InitializeNetwork(Action returnHome)
    {
        enabled = false;
        panel.SetActive(false); cancel.gameObject.SetActive(false);
        home.onClick.RemoveAllListeners(); home.onClick.AddListener(() => returnHome());
    }

    public void ShowNetworkResult(PvpMatchView view, string warning)
    {
        panel.SetActive(true);
        title.text = view.outcome == "Win" ? "VICTORY" : view.outcome == "Loss" ? "DEFEAT" : "DRAW";
        description.text = "PvP vs " + view.names[1 - view.seat] + "\nTurns: " + view.turn +
            "\n" + view.reason + (string.IsNullOrEmpty(warning) ? "" : "\n" + warning);
    }

    public void Initialize(DuelBoardReadOnlyDemo duelBoard, AppMatchRequest match, CardData[] opponent)
    {
        board = duelBoard; request = match; opponentDeck = opponent;
        bot = board.GetComponent<DuelEasyBotController>();
        panel.SetActive(false);
        home.onClick.RemoveAllListeners(); home.onClick.AddListener(ReturnHome);
        cancel.onClick.RemoveAllListeners(); cancel.onClick.AddListener(() =>
        {
            panel.SetActive(false); pendingExit = false;
            if (bot != null) bot.enabled = true;
        });
    }

    private void Update()
    {
        if (board == null || board.Session == null) return;
        usedAuto |= bot != null && (bot.IsAutoPlayerEnabled || !bot.IsEnabled);
        if (Input.GetKeyDown(KeyCode.Escape)) RequestExit();
        if (board.IsPresenting) return;
        if (board.Session.State.IsFinished && !resultShown)
        {
            resultShown = true; pendingExit = false;
            outcome = board.Session.State.WinnerPlayerIndex < 0 ? "Draw" :
                board.Session.State.WinnerPlayerIndex == 0 ? "Win" : "Loss";
            board.CloseCardWindows();
            panel.SetActive(true); cancel.gameObject.SetActive(false);
            title.text = outcome == "Draw" ? "DRAW" : outcome == "Win" ? "VICTORY" : "DEFEAT";
            description.text = request.deck.name + "\nTurns: " + board.Session.State.TurnNumber +
                (usedAuto ? "\nAuto / practice match" : "\nPvE vs " +
                    (request.difficulty == DuelBotDifficulty.Normal ? "Normal Bot" : "Easy Bot"));
            SaveResult();
        }
        else if (pendingExit && !resultShown && !panel.activeSelf)
        {
            board.CloseCardWindows();
            if (bot != null) bot.enabled = false;
            panel.SetActive(true); cancel.gameObject.SetActive(true);
            title.text = "RETURN HOME?";
            description.text = "This match will be recorded as abandoned. It will not affect win rate.";
        }
    }

    public void RequestExit() { if (!resultShown) pendingExit = true; }

    private void SaveResult()
    {
        if (saved) return;
        saved = LocalMatchArchive.Save(request, board.Session, opponentDeck, outcome, usedAuto, out string error);
        if (!saved) description.text = error + "\nPress HOME to retry saving.";
    }

    private void ReturnHome()
    {
        if (board == null || board.Session == null) return;
        if (!resultShown) outcome = "Abandoned";
        SaveResult();
        if (!saved) return;
        AppMatchLaunch.ReturningHome = true;
        SceneManager.LoadScene("AppShell");
    }

#if UNITY_EDITOR
    public void BuildEditorView(TMP_FontAsset font)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>();
        panel = new GameObject("ResultOrExitPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(.015f, .04f, .09f, .97f);
        title = Text("Title", "MATCH RESULT", font, 58, 200, 170);
        description = Text("Description", "Result, turn count and save status", font, 34, 0, 240);
        home = MakeButton("Home", "HOME", font, -220);
        cancel = MakeButton("Cancel", "CONTINUE DUEL", font, -365);
    }

    private TMP_Text Text(string objectName, string value, TMP_FontAsset font, int size, float y, float height)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(panel.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.08f, .5f); rect.anchorMax = new Vector2(.92f, .5f);
        rect.sizeDelta = new Vector2(0, height); rect.anchoredPosition = new Vector2(0, y);
        TMP_Text label = go.GetComponent<TMP_Text>();
        label.font = font; label.fontSize = size; label.text = value;
        label.alignment = TextAlignmentOptions.Center; label.enableWordWrapping = true;
        return label;
    }

    private Button MakeButton(string objectName, string value, TMP_FontAsset font, float y)
    {
        TMP_Text label = Text(objectName + "Label", value, font, 38, y, 110);
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(panel.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.15f, .5f); rect.anchorMax = new Vector2(.85f, .5f);
        rect.sizeDelta = new Vector2(0, 110); rect.anchoredPosition = new Vector2(0, y);
        go.GetComponent<Image>().color = new Color(.04f, .25f, .5f, 1);
        label.transform.SetParent(go.transform, false);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.raycastTarget = false;
        return go.GetComponent<Button>();
    }
#endif
}
