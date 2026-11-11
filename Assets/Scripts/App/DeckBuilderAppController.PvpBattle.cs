using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Network view only: receives perspective DTOs, never a remote GameState.
public sealed partial class DeckBuilderAppController
{
    private sealed class PvpTile { public Button button; public TMP_Text text; public Image art; }
    private GameObject pvpBattlePanel;
    private TMP_Text pvpTurn, pvpEnemyStats, pvpMyStats, pvpDetails, pvpActionHeading;
    private PvpTile[] pvpEnemyMonsters, pvpEnemySpells, pvpMyMonsters, pvpMySpells;
    private PvpTile pvpEnemyField, pvpMyField;
    private RectTransform pvpHand;
    private readonly List<GameObject> pvpHandCards = new List<GameObject>();
    private readonly Button[] pvpOptions = new Button[PvpDuelAuthority.PageSize];
    private Button pvpConfirm, pvpSurrender, pvpBattleBack, pvpPrevious, pvpNext;
    private Button pvpEnemyGrave, pvpMyGrave;
    private int pvpSelected = -1, pvpShownRevision = -1, pvpShownPage = -1;
    private bool pvpConfirmSurrender;
    private string pvpSavedMatch, pvpShownMatch;

    private void BuildPvpBattle()
    {
        RectTransform root = Panel(modalLayer.transform, "PvpBattle", Navy, 20, 40, 1040, 1830).rectTransform;
        pvpBattlePanel = root.gameObject;
        Label(root, "Title", "ONLINE DUEL", 43, Gold, 20, 5, 1000, 55);
        pvpTurn = PvpText(root, "Turn", 28, 20, 65, 1000, 55);
        pvpEnemyStats = PvpText(root, "Opponent", 27, 20, 125, 1000, 55);
        pvpEnemyField = PvpCardTile(root, "OpponentField", 20, 190, 650, 90);
        pvpEnemyGrave = ButtonAt(root, "OpponentGrave", "GRAVEYARD", 26,
            690, 190, 330, 90, Blue, () => OpenPvpGrave(false));
        pvpEnemySpells = PvpZoneRow(root, "OpponentSpell", 290, 105);
        pvpEnemyMonsters = PvpZoneRow(root, "OpponentMonster", 405, 135);
        pvpMyMonsters = PvpZoneRow(root, "MyMonster", 555, 135);
        pvpMySpells = PvpZoneRow(root, "MySpell", 700, 105);
        pvpMyField = PvpCardTile(root, "MyField", 20, 815, 650, 90);
        pvpMyGrave = ButtonAt(root, "MyGrave", "GRAVEYARD", 26,
            690, 815, 330, 90, Blue, () => OpenPvpGrave(true));
        pvpMyStats = PvpText(root, "You", 27, 20, 910, 1000, 55);
        RectTransform handViewport = Panel(root, "HandViewport", Blue, 20, 975, 1000, 145).rectTransform;
        handViewport.gameObject.AddComponent<RectMask2D>();
        pvpHand = NewRect(handViewport, "Cards");
        Position(pvpHand, 0, 0, 1000, 140);
        ScrollRect scroll = handViewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = handViewport; scroll.content = pvpHand; scroll.horizontal = true; scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        pvpActionHeading = PvpText(root, "ActionHeading", 28, 20, 1130, 1000, 55);
        for (int index = 0; index < pvpOptions.Length; index++)
        {
            int row = index;
            pvpOptions[index] = ButtonAt(root, "Action" + index, "", 25,
                20, 1190 + index * 55, 1000, 50, Blue, () => SelectPvpAction(row));
            pvpOptions[index].GetComponentInChildren<TMP_Text>().richText = false;
        }
        pvpPrevious = ButtonAt(root, "Previous", "PREVIOUS", 26, 20, 1530, 480, 65, Blue,
            () => pvpDuel.Page(pvpDuel.View.optionPage - 1));
        pvpNext = ButtonAt(root, "Next", "NEXT", 26, 520, 1530, 500, 65, Blue,
            () => pvpDuel.Page(pvpDuel.View.optionPage + 1));
        pvpDetails = PvpText(root, "Details", 25, 20, 1605, 1000, 110);
        pvpDetails.enableWordWrapping = true;
        pvpConfirm = ButtonAt(root, "Confirm", "CONFIRM", 27, 20, 1730, 300, 80, Blue, () =>
        {
            if (pvpSelected >= 0) pvpDuel.Act(pvpSelected);
        });
        pvpSurrender = ButtonAt(root, "Surrender", "SURRENDER", 23, 335, 1730, 225, 80, Blue, () =>
        {
            if (pvpConfirmSurrender) { pvpConfirmSurrender = false; pvpDuel.Surrender(); }
            else { pvpConfirmSurrender = true; pvpDetails.text = "Press SURRENDER again to concede this match."; }
        });
        pvpBattleBack = ButtonAt(root, "Room", "ROOM", 25, 575, 1730, 210, 80, Blue, ReturnFromPvp);
        ButtonAt(root, "History", "HISTORY", 25, 800, 1730, 220, 80, Blue,
            () => OpenPvpHistory(pvpBattlePanel));
        pvpBattlePanel.SetActive(false);
    }

    private TMP_Text PvpText(Transform parent, string name, int size, float x, float y, float width, float height)
    {
        TMP_Text text = Label(parent, name, "", size, White, x, y, width, height);
        text.richText = false;
        return text;
    }

    private PvpTile[] PvpZoneRow(Transform parent, string name, float y, float height)
    {
        var row = new PvpTile[3];
        for (int index = 0; index < row.Length; index++)
            row[index] = PvpCardTile(parent, name + index, 20 + index * 340, y, 320, height);
        return row;
    }

    private PvpTile PvpCardTile(Transform parent, string name, float x, float y, float width, float height)
    {
        Button button = ButtonAt(parent, name, "", 20, x, y, width, height, Blue, null);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        Position(text.rectTransform, 90, 0, width - 95, height);
        text.enableWordWrapping = true; text.richText = false;
        Image art = Panel(button.transform, "Artwork", Color.white, 5, 5, 80, height - 10);
        art.preserveAspect = true; art.raycastTarget = false;
        return new PvpTile { button = button, text = text, art = art };
    }

    private void ShowPvpCard(PvpTile tile, PvpPublicCard card)
    {
        // Unity's inline class serialization may reconstruct an empty slot as a default object.
        if (card != null && card.instanceId <= 0) card = null;
        tile.button.onClick.RemoveAllListeners();
        tile.button.interactable = card != null;
        tile.art.enabled = card != null && card.known && !card.faceDown &&
            !string.IsNullOrEmpty(card.cardId) && byId.TryGetValue(card.cardId, out CardData data) && data.Artwork != null;
        if (tile.art.enabled) tile.art.sprite = byId[card.cardId].Artwork;
        tile.text.text = card == null ? "EMPTY" : !card.known || card.faceDown ? "SET CARD" : card.name +
            (card.monster ? "\nHP " + card.hp + " | ATK " + card.atk + " | DEF " + card.def +
                "\nE " + card.energy + " / " + card.position : "");
        if (card == null) return;
        tile.button.onClick.AddListener(() =>
        {
            pvpConfirmSurrender = false;
            pvpDetails.text = card.known && !string.IsNullOrEmpty(card.cardId) && byId.TryGetValue(card.cardId, out CardData known)
                ? known.CardName + ": " + known.EffectText : "The opponent's face-down card is hidden.";
        });
    }

    private void RefreshPvpDiagnosticBattle(PvpMatchView view)
    {
        if (pvpBattlePanel == null) BuildPvpBattle();
        if (pvpPanel.activeSelf) OpenModalPanel(pvpBattlePanel);
        if (pvpShownMatch != view.matchId || pvpShownRevision != view.revision || pvpShownPage != view.optionPage)
        { pvpSelected = -1; pvpConfirmSurrender = false; }
        pvpShownMatch = view.matchId; pvpShownRevision = view.revision; pvpShownPage = view.optionPage;
        PvpPlayerView me = view.players[view.seat], enemy = view.players[1 - view.seat];
        pvpTurn.text = view.finished ? view.outcome.ToUpperInvariant() + " / " + view.reason :
            "Turn " + view.turn + " / " + view.phase + " / " +
            (view.interactionPlayer == view.seat ? "YOUR DECISION" : "OPPONENT'S DECISION");
        string Stats(PvpPlayerView player) => "LP " + player.health + " | E " + player.energy + "/" + player.maxEnergy +
            " | Deck " + player.deckCount + " | Hand " + player.handCount + " | Grave " + player.graveCount;
        pvpEnemyStats.text = view.names[1 - view.seat] + " - " + Stats(enemy);
        pvpMyStats.text = "YOU (Player " + (view.seat + 1) + ") - " + Stats(me);
        pvpEnemyGrave.GetComponentInChildren<TMP_Text>().text = "GRAVEYARD " + enemy.graveCount;
        pvpMyGrave.GetComponentInChildren<TMP_Text>().text = "GRAVEYARD " + me.graveCount;
        for (int index = 0; index < 3; index++)
        {
            ShowPvpCard(pvpEnemyMonsters[index], enemy.monsters[index]); ShowPvpCard(pvpEnemySpells[index], enemy.spells[index]);
            ShowPvpCard(pvpMyMonsters[index], me.monsters[index]); ShowPvpCard(pvpMySpells[index], me.spells[index]);
        }
        ShowPvpCard(pvpEnemyField, enemy.field); ShowPvpCard(pvpMyField, me.field);
        foreach (GameObject old in pvpHandCards) Destroy(old);
        pvpHandCards.Clear();
        pvpHand.sizeDelta = new Vector2(Mathf.Max(1000, me.hand.Length * 250), 140);
        for (int index = 0; index < me.hand.Length; index++)
        {
            PvpTile tile = PvpCardTile(pvpHand, "Hand" + index, index * 250, 0, 240, 140);
            ShowPvpCard(tile, me.hand[index]); pvpHandCards.Add(tile.button.gameObject);
        }
        bool canChoose = !view.finished && !pvpDuel.Pending && pvpRoom.Connected;
        for (int index = 0; index < pvpOptions.Length; index++)
        {
            bool available = index < view.options.Length;
            pvpOptions[index].gameObject.SetActive(available);
            pvpOptions[index].interactable = canChoose && available;
            if (available) pvpOptions[index].GetComponentInChildren<TMP_Text>().text = view.options[index].text;
        }
        pvpActionHeading.text = view.finished ? "MATCH ENDED" : pvpDuel.Pending ? "WAITING FOR HOST..." :
            view.optionTotal == 0 ? "WAITING FOR OPPONENT..." :
            "CHOOSE ACTION - PAGE " + (view.optionPage + 1) + "/" + ((view.optionTotal + 5) / 6);
        pvpPrevious.interactable = canChoose && view.optionPage > 0;
        pvpNext.interactable = canChoose && (view.optionPage + 1) * 6 < view.optionTotal;
        pvpConfirm.interactable = canChoose && pvpSelected >= 0;
        pvpSurrender.interactable = canChoose;
        pvpBattleBack.interactable = pvpDuel.CanLeaveResult;
        pvpDetails.text = !string.IsNullOrEmpty(pvpDuel.Notice) ? pvpDuel.Notice :
            view.log != null && view.log.Length > 0 ? view.log.Last() : "Select a card for details, or select an action and CONFIRM.";
        if (view.finished && !pvpDuel.CanLeaveResult && string.IsNullOrEmpty(pvpDuel.Notice))
            pvpDetails.text = "Waiting for the other device to acknowledge the result before leaving.";
        if (view.finished && pvpSavedMatch != view.matchId)
        {
            if (PvpLocalHistory.Save(view, out string error)) pvpSavedMatch = view.matchId;
            else pvpDetails.text = error + " / Press ROOM to retry.";
        }
    }

    private void SelectPvpAction(int row)
    {
        PvpMatchView view = pvpDuel.View;
        if (row >= view.options.Length) return;
        pvpSelected = view.options[row].index; pvpConfirmSurrender = false;
        pvpDetails.text = view.options[row].text;
        pvpConfirm.interactable = !pvpDuel.Pending && !view.finished && pvpRoom.Connected;
    }

    private void ReturnFromPvp()
    {
        PvpMatchView view = pvpDuel.View;
        if (view == null || !pvpDuel.CanLeaveResult) return;
        if (!PvpLocalHistory.Save(view, out string error)) { pvpDetails.text = error; return; }
        pvpRoom.Leave();
        pvpDuel.ClearFinished();
        OpenModalPanel(pvpPanel);
    }
}
