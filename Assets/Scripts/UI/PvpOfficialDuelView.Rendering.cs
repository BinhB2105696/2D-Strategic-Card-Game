using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class PvpOfficialDuelView
{
    private sealed class CardLocation
    {
        public PvpPublicCard card;
        public int owner, index;
        public CardZone zone;
        public RectTransform rect;
        public Transform anchor, status;
    }
    private readonly Dictionary<long, CardLocation> locations = new Dictionary<long, CardLocation>();
    private readonly List<GameObject> visuals = new List<GameObject>();
    private readonly List<RectTransform> enemyHand = new List<RectTransform>();

    private void Render(PvpMatchView view)
    {
        foreach (GameObject visual in visuals) if (visual != null) { visual.SetActive(false); Destroy(visual); }
        visuals.Clear(); locations.Clear(); enemyHand.Clear();
        RenderPlayer(view.seat, "Player", view);
        RenderPlayer(1 - view.seat, "Opp", view);
        TextAt(hudLayer, "TurnPhaseHUD/TurnText", "Turn " + view.turn);
        TextAt(hudLayer, "TurnPhaseHUD/PhaseText", view.phase + " Phase");
        TextAt(overlay, "SettingsOverlay/TurnPhaseText", "Turn " + view.turn + " - " + view.phase + " Phase");
        TextAt(overlay, "SettingsOverlay/PlayerHpText", "LP: " + view.players[view.seat].health);
        TextAt(overlay, "SettingsOverlay/OpponentHpText", "LP: " + view.players[1 - view.seat].health);
    }

    private void RenderPlayer(int owner, string side, PvpMatchView view)
    {
        PvpPlayerView player = view.players[owner];
        if (avatars != null && avatars.Length > 0 && view.avatars != null && view.avatars.Length == 2)
            assets.SetNetworkAvatar(side, avatars[Mathf.Clamp(view.avatars[owner], 0, avatars.Length - 1)]);
        TextAt(hudLayer, side + "_HUD/LpText", "LP:" + player.health);
        TextAt(hudLayer, side + "_HUD/NameText", view.names[owner]);
        TextAt(hudLayer, side + "_EnergyWidget/ValueText", player.energy.ToString());
        TextAt(boardLayer, side + "_PileColumn/" + side + "_DeckZone/CountText", player.deckCount.ToString());
        TextAt(boardLayer, side + "_PileColumn/" + side + "_GraveyardZone/CountText", player.graveCount.ToString());
        RenderZones(player.monsters, owner, side, true);
        RenderZones(player.spells, owner, side, false);
        Transform field = boardLayer.Find(side + "_FieldZone/CardAnchor");
        if (Present(player.field)) Spawn(player.field, owner, CardZone.FieldZone, 0, field, 1);
        Transform deck = boardLayer.Find(side + "_PileColumn/" + side + "_DeckZone/CardAnchor");
        if (player.deckCount > 0 && deck != null)
        {
            var back = new GameObject("RuntimeDeckBack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            back.transform.SetParent(deck, false); back.layer = deck.gameObject.layer;
            RectTransform rect = back.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(250, 350);
            Image image = back.GetComponent<Image>(); image.sprite = assets.NetworkCardBack; image.raycastTarget = false;
            visuals.Add(back);
        }
        Transform grave = boardLayer.Find(side + "_PileColumn/" + side + "_GraveyardZone/CardAnchor");
        for (int index = 0; index < player.grave.Length; index++)
        {
            PvpPublicCard card = player.grave[index];
            locations[card.instanceId] = new CardLocation { card = card, owner = owner, index = index, zone = CardZone.Graveyard, anchor = grave };
        }
        if (player.grave.Length > 0) Spawn(player.grave[0], owner, CardZone.Graveyard, 0, grave, 1, false);
        RenderHand(player, owner, side, view.seat == owner);
    }

    private void RenderZones(PvpPublicCard[] cards, int owner, string side, bool monster)
    {
        string type = monster ? "Monster" : "SpellTrap";
        for (int index = 0; index < cards.Length; index++)
        {
            int displayIndex = side == "Opp" ? cards.Length - 1 - index : index;
            Transform zone = boardLayer.Find(side + "_" + type + "Row/" + side + "_" + type + "Zone_" + displayIndex);
            if (zone == null) throw new InvalidOperationException("Authored zone is missing: " + side + type + displayIndex);
            int selectedZone = index;
            Button tap = zone.GetComponent<Button>() ?? zone.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.navigation = new Navigation { mode = Navigation.Mode.None };
            tap.targetGraphic = zone.GetComponent<Graphic>();
            if (tap.targetGraphic != null) tap.targetGraphic.raycastTarget = true;
            tap.onClick = new Button.ButtonClickedEvent(); tap.onClick.AddListener(() => TapZone(owner, selectedZone, monster));
            if (monster)
            {
                DuelMonsterZoneStatusView status = zone.GetComponent<DuelMonsterZoneStatusView>() ?? zone.gameObject.AddComponent<DuelMonsterZoneStatusView>();
                status.Initialize(assets.NetworkFont); status.SetNetworkMonster(Present(cards[index]) ? cards[index] : null);
            }
            if (!Present(cards[index])) continue;
            CardLocation location = Spawn(cards[index], owner, monster ? CardZone.MonsterZone : CardZone.SpellTrapZone,
                index, zone.Find("CardAnchor"), 1);
            if (monster) location.status = zone.Find("RuntimeMonsterStatus");
            PvpMatchView view = displayed;
            if (monster && owner == view.seat && view.interactionPlayer == view.seat &&
                view.phase == "Battle" && !view.response && !view.trigger && !view.finished &&
                view.choices.sources.Contains(cards[index].instanceId))
                visuals.Add(DuelAttackReadyGlow.Create(location.anchor));
        }
    }

    private void RenderHand(PvpPlayerView player, int owner, string side, bool own)
    {
        Transform content = boardLayer.Find(side + "_HandArea/HandContent");
        Transform template = content.Find("HandCardSlot_Template"); template.gameObject.SetActive(false);
        DuelHandLayoutController layout = content.GetComponent<DuelHandLayoutController>() ?? content.gameObject.AddComponent<DuelHandLayoutController>();
        layout.BeginLayout(own, !own);
        int count = own ? player.hand.Length : player.handCount;
        for (int index = 0; index < count; index++)
        {
            GameObject slot = Instantiate(template.gameObject, content); visuals.Add(slot);
            slot.name = "PvpHandSlot_" + index;
            Transform placeholder = slot.transform.Find("PlaceholderText"); if (placeholder != null) placeholder.gameObject.SetActive(false);
            if (own) Spawn(player.hand[index], owner, CardZone.Hand, index, slot.transform, .7f);
            else
            {
                GameObject hidden = assets.CreateNetworkCard(null, slot.transform, .7f, true, false);
                enemyHand.Add(hidden.GetComponent<RectTransform>());
                BindTap(hidden, () => windows.ShowNetworkActions("Opponent's hand is hidden.", new PvpUiOption[0]));
            }
            layout.AddSlot(slot.GetComponent<RectTransform>()); slot.SetActive(true);
        }
        layout.ApplyLayout();
    }

    private CardLocation Spawn(PvpPublicCard card, int owner, CardZone zone, int index, Transform anchor, float scale, bool tap = true)
    {
        if (anchor == null) throw new InvalidOperationException("Authored card anchor is missing.");
        catalog.TryGetValue(card.cardId ?? "", out CardData data);
        GameObject visual = assets.CreateNetworkCard(data, anchor, scale, card.faceDown || !card.known, card.position == "Defense");
        visuals.Add(visual);
        var location = new CardLocation { card = card, owner = owner, zone = zone, index = index,
            rect = visual.GetComponent<RectTransform>(), anchor = anchor };
        locations[card.instanceId] = location;
        if (tap) BindTap(visual, () => TapCard(location));
        return location;
    }

    private static bool Present(PvpPublicCard card) => card != null && card.instanceId > 0;
    private static void BindTap(GameObject target, Action action)
    {
        Button button = target.GetComponent<Button>() ?? target.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.targetGraphic = target.GetComponent<Graphic>() ?? target.GetComponentInChildren<Graphic>();
        if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
        button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(() => action());
    }
    private static void TextAt(Transform root, string path, string value)
    {
        Transform target = root.Find(path); TMP_Text text = target != null ? target.GetComponent<TMP_Text>() : null;
        if (text != null) { text.richText = false; text.text = value; }
    }

    private CardInstance PresentationCard(CardLocation location)
    {
        PvpPublicCard data = location.card;
        if (!data.known || !definitions.TryGetValue(data.cardId, out CardDefinition definition)) return null;
        var card = new CardInstance(data.instanceId, location.owner, definition);
        if (location.zone == CardZone.MonsterZone) card.Summon(data.position == "Defense" ? BattlePosition.Defense : BattlePosition.Attack);
        else card.MoveToZone(location.zone, data.faceDown);
        if (definition.IsMonster)
        {
            card.ModifyMaximumHealth(data.maxHp - definition.BaseHealth);
            card.ReceiveDamage(Mathf.Max(0, card.MaximumHealth - data.hp));
            card.ModifyAttack(data.atk - definition.BaseAttack); card.ModifyDefense(data.def - definition.BaseDefense);
            if (data.energy > 0) card.AllocateEnergy(data.energy);
        }
        return card; // A detached presentation object, never submitted to GameRules or the host.
    }

    private void BindPile(string side, bool own)
    {
        Transform grave = boardLayer.Find(side + "_PileColumn/" + side + "_GraveyardZone");
        BindTap(grave.gameObject, () =>
        {
            int owner = own ? coordinator.View.seat : 1 - coordinator.View.seat;
            var cards = coordinator.View.players[owner].grave.Reverse().Select(data => PresentationCard(locations[data.instanceId])).ToArray();
            windows.ShowNetworkPile(own ? "Your Graveyard" : "Opponent Graveyard", cards);
        });
        Transform deck = boardLayer.Find(side + "_PileColumn/" + side + "_DeckZone");
        BindTap(deck.gameObject, () =>
        {
            if (own) { hud.settingsPanel.SetActive(true); ShowTab(1); }
            else windows.ShowNetworkActions("Opponent deck: " + coordinator.View.players[1 - coordinator.View.seat].deckCount + " cards.", new PvpUiOption[0]);
        });
    }
}
