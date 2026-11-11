using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class DeckBuilderAppController
{
    private PvpRoomConnection pvpRoom;
    private PvpDuelCoordinator pvpDuel;
    private GameObject pvpPanel;
    private TMP_Text pvpCode, pvpStatus;
    private TMP_InputField pvpInput;
    private Button pvpCreate, pvpJoin, pvpPing, pvpCopy, pvpLeave;

    private void OpenPvpRoom()
    {
        if (profile.decks.Count == 0) { SetMessage("Save a valid deck first."); return; }
        SavedDeck selected = profile.decks[profile.selectedDeck];
        if (!LocalDeckStore.Validate(selected, byId, GameConfig.CreateDefault(), out string error))
        { SetMessage(error); return; }
        if (pvpRoom == null)
        {
            pvpRoom = gameObject.AddComponent<PvpRoomConnection>();
            pvpRoom.Changed += RefreshPvpRoom;
        }
        if (pvpDuel == null)
        {
            try
            {
                pvpDuel = gameObject.AddComponent<PvpDuelCoordinator>();
                pvpDuel.Initialize(pvpRoom, byId, selected, profile.guestName, profile.avatarIndex);
                pvpDuel.Changed += RefreshPvpRoom;
            }
            catch (System.Exception exception)
            {
                if (pvpDuel != null) Destroy(pvpDuel);
                pvpDuel = null;
                SetMessage("PvP card/rule compatibility check failed: " + exception.Message);
                return;
            }
        }
        pvpDuel.SetLocalDeck(selected, profile.guestName, profile.avatarIndex);
        if (pvpPanel == null)
        {
            RectTransform panel = Panel(modalLayer.transform, "PvpRoomPanel", Navy,
                70, 330, 940, 1270).rectTransform;
            pvpPanel = panel.gameObject;
            Label(panel, "Title", "PVP ROOM", 48, Gold, 35, 35, 870, 85);
            Label(panel, "Scope", "BOTH PLAYERS PRESS READY TO START", 26,
                Cyan, 35, 125, 870, 65);
            pvpCode = Label(panel, "RoomCode", "ROOM CODE: ------", 43, Gold, 35, 210, 870, 80);
            pvpCopy = ButtonAt(panel, "Copy", "COPY CODE", 30, 270, 310, 400, 80, Blue,
                () => GUIUtility.systemCopyBuffer = pvpRoom.Code);
            pvpCreate = ButtonAt(panel, "Create", "CREATE ROOM", 36,
                70, 420, 800, 105, Blue, async () =>
                { if (pvpRoom.Connected) pvpDuel.Ready(); else await pvpRoom.CreateAsync(); });
            pvpInput = Input(panel, "JoinCode", "", 70, 555, 800, 85, "ENTER ROOM CODE");
            pvpInput.characterLimit = PvpRoomProtocol.MaxCodeLength;
            pvpInput.contentType = TMP_InputField.ContentType.Alphanumeric;
            pvpJoin = ButtonAt(panel, "Join", "JOIN ROOM", 36,
                70, 665, 800, 105, Blue, async () => await pvpRoom.JoinAsync(pvpInput.text));
            pvpStatus = Label(panel, "Status", "", 29, White, 45, 790, 850, 150);
            pvpStatus.enableWordWrapping = true;
            pvpStatus.overflowMode = TextOverflowModes.Overflow;
            pvpPing = ButtonAt(panel, "Ping", "SEND TEST SIGNAL", 31,
                70, 960, 390, 90, Blue, () => pvpRoom.Ping());
            ButtonAt(panel, "History", "PVP HISTORY", 29,
                480, 960, 390, 90, Blue, () => OpenPvpHistory(pvpPanel));
            pvpLeave = ButtonAt(panel, "Leave", "LEAVE / CANCEL", 30,
                70, 1075, 390, 90, Blue, () => pvpRoom.Leave());
            ButtonAt(panel, "Back", "BACK", 30, 480, 1075, 390, 90, Blue, () =>
            {
                pvpRoom.Leave();
                CloseModal();
            });
            Label(panel, "Hint", "Keep the host app open. Closing it ends the room.",
                25, Cyan, 35, 1190, 870, 55);
        }
        RefreshPvpRoom();
        OpenModalPanel(pvpPanel);
    }

    private void RefreshPvpRoom()
    {
        if (pvpPanel == null || pvpRoom == null) return;
        pvpCode.text = "ROOM CODE: " + (string.IsNullOrEmpty(pvpRoom.Code) ? "------" : pvpRoom.Code);
        PvpMatchView view = pvpDuel?.View;
        pvpStatus.text = !string.IsNullOrEmpty(pvpDuel?.Notice) ? pvpDuel.Notice : pvpRoom.Message;
        if (view != null && !view.started && pvpRoom.Connected)
            pvpStatus.text += "\nYou: " + (view.myReady ? "READY" : "not ready") +
                " / Opponent: " + (view.opponentReady ? "READY" : "not ready");
        pvpCreate.GetComponentInChildren<TMP_Text>().text = pvpRoom.Connected
            ? (view != null && view.myReady ? "READY - WAITING" : "READY TO DUEL") : "CREATE ROOM";
        pvpCreate.interactable = pvpRoom.CanOpen || (pvpDuel != null && pvpDuel.CanReady);
        pvpJoin.interactable = pvpInput.interactable = pvpRoom.CanOpen;
        pvpPing.interactable = pvpRoom.CanPing;
        pvpCopy.interactable = !string.IsNullOrEmpty(pvpRoom.Code);
        pvpLeave.interactable = !pvpRoom.CanOpen;
        if (view != null && view.started) RefreshPvpBattle(view);
    }
}
