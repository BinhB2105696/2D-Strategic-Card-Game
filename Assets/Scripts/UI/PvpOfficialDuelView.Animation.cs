using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class PvpOfficialDuelView
{
    private IEnumerator Present(PvpMatchView view)
    {
        busy = true; windows.CloseAllCardWindows(); hud.confirmation.SetActive(false);
        workflow = false; requestedKey = ""; ClearHighlights();
        bool opening = displayed == null;
        bool phaseChanged = opening || displayed.turn != view.turn || displayed.phase != view.phase;
        if (!opening)
        {
            if (view.actionSequence > lastSequence && view.lastSource > 0 && locations.TryGetValue(view.lastSource, out CardLocation source))
            {
                if (view.lastAction == "Attack" && source.rect != null)
                {
                    Vector3 target = view.lastTarget > 0 && locations.TryGetValue(view.lastTarget, out CardLocation defender) && defender.rect != null
                        ? defender.rect.position : hudLayer.Find((view.lastActor == view.seat ? "Opp" : "Player") + "_HUD/AvatarFrame").position;
                    yield return GhostMotion(source, Vector3.Lerp(source.rect.position, target, .78f), Vector3.one, true);
                }
                else if (view.lastAction == "ChangeBattlePosition" && source.rect != null)
                {
                    PvpPublicCard after = ViewCards(view).FirstOrDefault(card => card.instanceId == source.card.instanceId);
                    if (after != null) yield return motion.PlayNetworkRotation(source.rect,
                        source.rect.parent.rotation * Quaternion.Euler(0, 0, after.position == "Defense" ? 90 : 0));
                }
            }
            foreach (CardLocation location in locations.Values.ToArray())
            {
                if (location.status == null || !location.card.monster) continue;
                PvpPublicCard next = ViewCards(view).FirstOrDefault(card => card.instanceId == location.card.instanceId);
                int hp = next != null ? next.hp : 0;
                TMP_Text label = location.status.Find("HealthText")?.GetComponent<TMP_Text>();
                if (hp != location.card.hp) yield return motion.PlayNetworkNumber(label, "HP ", location.card.hp, hp, next?.maxHp ?? location.card.maxHp);
            }
            for (int player = 0; player < 2; player++)
            {
                string side = player == view.seat ? "Player" : "Opp";
                TMP_Text lp = hudLayer.Find(side + "_HUD/LpText")?.GetComponent<TMP_Text>();
                yield return motion.PlayNetworkNumber(lp, "LP:", displayed.players[player].health, view.players[player].health);
                TMP_Text energy = hudLayer.Find(side + "_EnergyWidget/ValueText")?.GetComponent<TMP_Text>();
                yield return motion.PlayNetworkNumber(energy, "", displayed.players[player].energy, view.players[player].energy);
            }
            foreach (CardLocation location in locations.Values.ToArray())
            {
                if (location.rect == null || location.zone == CardZone.Graveyard) continue;
                CardZone nextZone = ZoneOf(view, location.card.instanceId);
                if (nextZone == location.zone) continue;
                Transform destination = Destination(view, location.card.instanceId);
                if (destination != null) yield return GhostMotion(location, destination.position, Vector3.one, false);
            }
        }
        var oldIds = new HashSet<long>(locations.Keys);
        int previousEnemyHand = displayed == null ? 0 : displayed.players[1 - displayed.seat].handCount;
        displayed = view; Render(view); lastRevision = view.revision; lastSequence = view.actionSequence;
        foreach (CardLocation location in locations.Values.ToArray())
        {
            if (oldIds.Contains(location.card.instanceId) || location.rect == null || location.zone == CardZone.Graveyard) continue;
            string side = location.owner == view.seat ? "Player" : "Opp";
            Transform start = boardLayer.Find(side + "_PileColumn/" + side + "_DeckZone/CardAnchor");
            if (location.zone != CardZone.Hand) start = boardLayer.Find(side + "_HandArea");
            yield return GhostMotion(location, location.rect.position, Vector3.one, false, start.position, opening);
        }
        for (int index = opening ? 0 : previousEnemyHand; index < enemyHand.Count; index++)
        {
            var hidden = new CardLocation { rect = enemyHand[index], zone = CardZone.Hand, owner = 1 - view.seat };
            Transform deck = boardLayer.Find("Opp_PileColumn/Opp_DeckZone/CardAnchor");
            yield return GhostMotion(hidden, hidden.rect.position, Vector3.one, false, deck.position, opening);
        }
        if (phaseChanged)
            yield return motion.PlayNetworkBanner(opening ? "DUEL START" : "Turn " + view.turn + " - " + view.phase);
        busy = false;
        if (!boardReadySent && !view.finished) { boardReadySent = true; coordinator.BoardReady(); }
        ProcessChoices(coordinator.View); RefreshControls();
        if (queued != null) { PvpMatchView pending = queued; queued = null; Receive(pending); }
    }

    private IEnumerator GhostMotion(CardLocation source, Vector3 target, Vector3 scale, bool attack, Vector3? from = null, bool fast = false)
    {
        if (source.rect == null) yield break;
        var root = new GameObject("PvpAcceptedMotion", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(overlay, false); root.layer = gameObject.layer;
        RectTransform group = root.GetComponent<RectTransform>(); group.position = from ?? source.rect.position;
        root.GetComponent<Canvas>().overrideSorting = true; root.GetComponent<Canvas>().sortingOrder = 18;
        GameObject ghost = Instantiate(source.rect.gameObject, group);
        RectTransform rect = ghost.GetComponent<RectTransform>();
        rect.position = group.position; rect.rotation = source.rect.rotation;
        Vector3 parentScale = group.lossyScale;
        rect.localScale = new Vector3(source.rect.lossyScale.x / parentScale.x, source.rect.lossyScale.y / parentScale.y, 1);
        foreach (Graphic graphic in ghost.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        if (source.status != null)
        {
            GameObject stats = Instantiate(source.status.gameObject, group);
            RectTransform statsRect = stats.GetComponent<RectTransform>();
            statsRect.anchorMin = statsRect.anchorMax = statsRect.pivot = new Vector2(.5f, .5f);
            statsRect.sizeDelta = source.status.GetComponent<RectTransform>().rect.size;
            stats.transform.position = group.position + (source.status.position - source.rect.position);
            stats.transform.rotation = source.status.rotation;
            Vector3 world = source.status.lossyScale;
            stats.transform.localScale = new Vector3(world.x / parentScale.x, world.y / parentScale.y, 1);
            Canvas canvas = stats.GetComponent<Canvas>(); if (canvas != null) canvas.overrideSorting = false;
            int shownHealth = source.card.hp;
            TMP_Text originalHealth = source.status.Find("HealthText")?.GetComponent<TMP_Text>();
            if (originalHealth != null && int.TryParse(originalHealth.text.Replace("HP ", ""), out int currentHealth)) shownHealth = currentHealth;
            DuelMonsterZoneStatusView.SetHealthText(stats.transform.Find("HealthText")?.GetComponent<TMP_Text>(),
                shownHealth, source.card.maxHp);
            stats.SetActive(true);
        }
        source.rect.gameObject.SetActive(false);
        bool statusActive = source.status != null && source.status.gameObject.activeSelf;
        if (source.status != null) source.status.gameObject.SetActive(false);
        yield return motion.PlayNetworkMotion(group, target, scale, attack, fast);
        if (source.rect != null) source.rect.gameObject.SetActive(true);
        if (source.status != null) source.status.gameObject.SetActive(statusActive);
        Destroy(root);
    }

    private static IEnumerable<PvpPublicCard> ViewCards(PvpMatchView view) => view.players.SelectMany(player =>
        player.hand.Concat(player.monsters).Concat(player.spells).Concat(player.grave).Concat(new[] { player.field })).Where(Present);

    private CardZone ZoneOf(PvpMatchView view, long id)
    {
        foreach (PvpPlayerView player in view.players)
        {
            if (player.grave.Any(card => Present(card) && card.instanceId == id)) return CardZone.Graveyard;
            if (player.monsters.Any(card => Present(card) && card.instanceId == id)) return CardZone.MonsterZone;
            if (player.spells.Any(card => Present(card) && card.instanceId == id)) return CardZone.SpellTrapZone;
            if (Present(player.field) && player.field.instanceId == id) return CardZone.FieldZone;
            if (player.hand.Any(card => card.instanceId == id)) return CardZone.Hand;
        }
        return CardZone.Deck;
    }

    private Transform Destination(PvpMatchView view, long id)
    {
        for (int owner = 0; owner < 2; owner++)
        {
            PvpPlayerView player = view.players[owner]; string side = owner == view.seat ? "Player" : "Opp";
            if (player.grave.Any(card => card.instanceId == id)) return boardLayer.Find(side + "_PileColumn/" + side + "_GraveyardZone/CardAnchor");
            if (player.hand.Any(card => card.instanceId == id)) return boardLayer.Find(side + "_HandArea");
            if (Present(player.field) && player.field.instanceId == id) return boardLayer.Find(side + "_FieldZone/CardAnchor");
            for (int index = 0; index < 3; index++)
            {
                int display = owner == view.seat ? index : 2 - index;
                if (Present(player.monsters[index]) && player.monsters[index].instanceId == id)
                    return boardLayer.Find(side + "_MonsterRow/" + side + "_MonsterZone_" + display + "/CardAnchor");
                if (Present(player.spells[index]) && player.spells[index].instanceId == id)
                    return boardLayer.Find(side + "_SpellTrapRow/" + side + "_SpellTrapZone_" + display + "/CardAnchor");
            }
        }
        return null;
    }
}
