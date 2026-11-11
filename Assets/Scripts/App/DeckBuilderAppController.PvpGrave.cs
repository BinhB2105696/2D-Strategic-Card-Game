using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class DeckBuilderAppController
{
    private GameObject pvpGravePanel;
    private RectTransform pvpGraveContent;
    private TMP_Text pvpGraveTitle, pvpGraveDetails;
    private readonly List<GameObject> pvpGraveCards = new List<GameObject>();

    private void OpenPvpGrave(bool own)
    {
        PvpMatchView view = pvpDuel.View;
        if (view == null || !view.started) return;
        if (pvpGravePanel == null)
        {
            RectTransform panel = Panel(modalLayer.transform, "PvpGraveyard", Navy, 40, 230, 1000, 1450).rectTransform;
            pvpGravePanel = panel.gameObject;
            pvpGraveTitle = PvpText(panel, "Title", 36, 30, 30, 940, 85);
            RectTransform viewport = Panel(panel, "Viewport", Blue, 40, 140, 920, 920).rectTransform;
            viewport.gameObject.AddComponent<RectMask2D>();
            pvpGraveContent = NewRect(viewport, "Cards");
            Position(pvpGraveContent, 0, 0, 920, 920);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = pvpGraveContent;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            pvpGraveDetails = PvpText(panel, "Details", 27, 40, 1090, 920, 190);
            pvpGraveDetails.enableWordWrapping = true;
            ButtonAt(panel, "Back", "BACK", 34, 200, 1300, 600, 100, Blue,
                () => OpenModalPanel(pvpBattlePanel));
        }
        PvpPublicCard[] cards = view.players[own ? view.seat : 1 - view.seat].grave;
        pvpGraveTitle.text = (own ? "YOUR" : "OPPONENT'S") + " GRAVEYARD - NEWEST FIRST";
        pvpGraveDetails.text = cards.Length == 0 ? "The Graveyard is empty." : "Select a card to read its effect.";
        foreach (GameObject old in pvpGraveCards) Destroy(old);
        pvpGraveCards.Clear();
        pvpGraveContent.anchoredPosition = Vector2.zero;
        pvpGraveContent.sizeDelta = new Vector2(920, Mathf.Max(920, cards.Length * 155));
        for (int index = 0; index < cards.Length; index++)
        {
            PvpPublicCard card = cards[index];
            PvpTile tile = PvpCardTile(pvpGraveContent, "Card" + index, 0, index * 155, 920, 145);
            ShowPvpCard(tile, card);
            tile.button.onClick.RemoveAllListeners();
            tile.button.onClick.AddListener(() => pvpGraveDetails.text =
                byId.TryGetValue(card.cardId, out CardData data) ? data.CardName + ": " + data.EffectText : card.name);
            pvpGraveCards.Add(tile.button.gameObject);
        }
        OpenModalPanel(pvpGravePanel);
    }
}
