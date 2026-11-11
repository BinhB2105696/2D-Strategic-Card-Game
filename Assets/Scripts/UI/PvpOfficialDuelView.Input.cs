using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class PvpOfficialDuelView
{
    private bool workflow;
    private bool selectionPreconfirmed;
    private PvpActionQuery activeQuery;
    private string requestedKey = "";
    private int promptRevision = -1;
    private readonly List<GameObject> highlights = new List<GameObject>();

    private void Begin(long source, string type = "", bool confirmed = false)
    {
        if (!CanAct) return;
        workflow = true; activeQuery = new PvpActionQuery { source = source, type = type };
        selectionPreconfirmed = confirmed;
        requestedKey = ""; hud.phases.SetActive(false); hud.settingsPanel.SetActive(false);
        coordinator.Filter(activeQuery);
    }

    private void ChangeQuery(Action<PvpActionQuery> edit)
    {
        if (!CanAct || activeQuery == null) return;
        PvpActionQuery next = JsonUtility.FromJson<PvpActionQuery>(JsonUtility.ToJson(activeQuery));
        edit(next); activeQuery = next; requestedKey = ""; coordinator.Filter(next);
    }

    private void ProcessChoices(PvpMatchView view)
    {
        if (view == null || !CanAct || view.choices == null) return;
        if (!workflow)
        {
            if ((view.response || view.trigger) && promptRevision != view.revision)
            {
                promptRevision = view.revision;
                hud.settingsPanel.SetActive(false); hud.phases.SetActive(false);
                var options = new List<PvpUiOption>();
                if (view.choices.sources.Length > 0) options.Add(new PvpUiOption("CHOOSE CARD", ShowResponseChoices));
                string pass = view.response ? "PassResponse" : "DeclineTriggeredEffect";
                options.Add(new PvpUiOption(view.response ? "PASS" : "DECLINE", () => Begin(0, pass, true)));
                windows.ShowNetworkActions(view.response ? "Response window" : "Triggered effect", options);
            }
            return;
        }
        string key = JsonUtility.ToJson(view.query) + "@" + view.revision + "/" + view.optionPage;
        if (JsonUtility.ToJson(view.query) != JsonUtility.ToJson(activeQuery) || requestedKey == key) return;
        requestedKey = key;
        ClearHighlights();
        if (string.IsNullOrEmpty(activeQuery.type))
        {
            var root = view.choices.types.Select(type => new PvpUiOption(Friendly(type), () => Begin(activeQuery.source, type))).ToList();
            if (locations.TryGetValue(activeQuery.source, out CardLocation card) && card.card.known)
                root.Add(new PvpUiOption("CARD INFO", () => ShowInformation(card)));
            windows.ShowNetworkActions(card?.card.name ?? "Card actions", root);
            return;
        }
        if (view.optionTotal == 0)
        { windows.ShowNetworkActions("This action is no longer available.", new PvpUiOption[0]); workflow = false; return; }
        PvpActionChoices choices = view.choices;
        if (choices.positions.Length > 0 && string.IsNullOrEmpty(activeQuery.position))
        {
            if (choices.positions.Length == 1) { ChangeQuery(query => query.position = choices.positions[0]); return; }
            windows.ShowNetworkActions("Choose battle position", choices.positions.Select(position =>
                new PvpUiOption(position.ToUpperInvariant(), () => ChangeQuery(query => query.position = position))).ToArray());
            return;
        }
        if (activeQuery.tributes.Length < choices.tributeCount)
        {
            SelectCards("Choose tribute/cost cards (" + activeQuery.tributes.Length + "/" + choices.tributeCount + ")",
                choices.tributes.Except(activeQuery.tributes), id => ChangeQuery(query => query.tributes = query.tributes.Concat(new[] { id }).ToArray()));
            return;
        }
        if (activeQuery.type == "AllocateEnergy" && activeQuery.amount < 0)
        {
            windows.ShowNetworkEnergy("Allocate Energy", choices.amounts, amount =>
            { selectionPreconfirmed = true; ChangeQuery(query => query.amount = amount); });
            return;
        }
        if (activeQuery.targets.Length < choices.targetCount)
        {
            SelectCards("Choose targets (" + activeQuery.targets.Length + "/" + choices.targetCount + ")",
                choices.targets.Except(activeQuery.targets), id => ChangeQuery(query => query.targets = query.targets.Concat(new[] { id }).ToArray()));
            return;
        }
        if (choices.zones.Length > 1 && activeQuery.zone < 0)
        {
            foreach (int zone in choices.zones)
            {
                string row = activeQuery.type == "SummonMonster" ? "Monster" : "SpellTrap";
                Transform target = boardLayer.Find("Player_" + row + "Row/Player_" + row + "Zone_" + zone);
                Mark(target.gameObject, false);
            }
            windows.ShowNetworkActions("Choose a zone", choices.zones.Select(zone =>
                new PvpUiOption(zone == 0 ? "LEFT" : zone == 1 ? "MIDDLE" : "RIGHT",
                    () => ChangeQuery(query => query.zone = zone))).ToArray());
            return;
        }
        if (view.optionTotal == 1)
        {
            PvpActionOption option = view.options[0];
            windows.CloseAllCardWindows();
            if (selectionPreconfirmed)
            { selectionPreconfirmed = false; workflow = false; ClearHighlights(); coordinator.Act(option.index); return; }
            Confirm(option.text + "?", () => { workflow = false; ClearHighlights(); coordinator.Act(option.index); });
            return;
        }
        // Ordered/multi-target variants still use the authored action panel, scoped to the selected card.
        var variants = view.options.Select(option => new PvpUiOption(option.text, () =>
            Confirm(option.text + "?", () => { workflow = false; coordinator.Act(option.index); }))).ToList();
        if (view.optionPage > 0) variants.Add(new PvpUiOption("PREVIOUS", () => { requestedKey = ""; coordinator.Page(view.optionPage - 1); }));
        if ((view.optionPage + 1) * PvpDuelAuthority.PageSize < view.optionTotal)
            variants.Add(new PvpUiOption("NEXT", () => { requestedKey = ""; coordinator.Page(view.optionPage + 1); }));
        windows.ShowNetworkActions("Choose action details", variants);
    }

    private void TapCard(CardLocation location)
    {
        if (busy) return;
        PvpMatchView view = coordinator.View;
        if (CanAct && workflow && activeQuery != null)
        {
            if (activeQuery.tributes.Length < view.choices.tributeCount && view.choices.tributes.Contains(location.card.instanceId))
            { ChangeQuery(query => query.tributes = query.tributes.Concat(new[] { location.card.instanceId }).Distinct().ToArray()); return; }
            if (activeQuery.targets.Length < view.choices.targetCount && view.choices.targets.Contains(location.card.instanceId))
            { ChangeQuery(query => query.targets = query.targets.Concat(new[] { location.card.instanceId }).Distinct().ToArray()); return; }
            if (view.choices.zones.Length > 1 && activeQuery.zone < 0 && location.owner == view.seat &&
                (location.zone == CardZone.MonsterZone || location.zone == CardZone.SpellTrapZone))
            { TapZone(location.owner, location.index, location.zone == CardZone.MonsterZone); return; }
        }
        if (CanAct && location.owner == view.seat && location.card.known) Begin(location.card.instanceId);
        else ShowInformation(location);
    }

    private void TapZone(int owner, int index, bool monster)
    {
        if (!CanAct || !workflow || activeQuery == null || owner != coordinator.View.seat) return;
        if (monster != (activeQuery.type == "SummonMonster")) return;
        if (activeQuery.tributes.Length < coordinator.View.choices.tributeCount ||
            activeQuery.targets.Length < coordinator.View.choices.targetCount) return;
        if (coordinator.View.choices.zones.Contains(index)) ChangeQuery(query => query.zone = index);
    }

    private void ShowInformation(CardLocation location)
    {
        CardInstance card = PresentationCard(location);
        if (card == null) { windows.ShowNetworkActions("This face-down card is hidden.", new PvpUiOption[0]); return; }
        windows.ShowNetworkDetail(card, "Card information", CanAct ? (Action)(() => Begin(location.card.instanceId)) : null);
    }

    private void ShowResponseChoices()
    {
        var cards = coordinator.View.choices.sources.Where(locations.ContainsKey)
            .Select(id => PresentationCard(locations[id])).Where(card => card != null).ToArray();
        windows.ShowNetworkCardChoices("Choose a response card", cards, card => Begin(card.InstanceId));
    }

    private void SelectCards(string title, IEnumerable<long> ids, Action<long> choose)
    {
        long[] available = ids.Where(locations.ContainsKey).ToArray();
        if (available.Any(id => !locations[id].card.known))
        {
            windows.ShowNetworkActions(title, available.Select(id => new PvpUiOption(
                locations[id].card.known ? locations[id].card.name : "Face-down card",
                () => choose(id))).ToArray());
            return;
        }
        var cards = available.Select(id => PresentationCard(locations[id])).Where(card => card != null).ToArray();
        windows.ShowNetworkTargets(title, cards, card =>
        {
            CardLocation location = locations[card.InstanceId];
            return (location.owner == coordinator.View.seat ? "Your " : "Opponent ") +
                (location.zone == CardZone.Hand ? "Hand" : location.index == 0 ? "Left" : location.index == 1 ? "Middle" : "Right");
        }, card => choose(card.InstanceId));
    }

    private static string Friendly(string value) => Regex.Replace(value, "(\\B[A-Z])", " $1").ToUpperInvariant();
    private void Highlight(IEnumerable<long> allowed, IEnumerable<long> selected)
    {
        foreach (long id in allowed)
            if (locations.TryGetValue(id, out CardLocation card) && card.rect != null)
                Mark(card.rect.gameObject, selected.Contains(id));
    }
    private void Mark(GameObject target, bool selected)
    {
        Transform parent = target.transform.Find("CardAnchor") ?? target.transform;
        GameObject frame = DuelAttackReadyGlow.Create(parent);
        frame.GetComponent<DuelAttackReadyGlow>().enabled = false;
        foreach (Image edge in frame.GetComponentsInChildren<Image>())
        { edge.color = selected ? new Color32(255, 215, 76, 255) : new Color32(52, 213, 235, 255); edge.raycastTarget = false; }
        highlights.Add(frame);
    }
    private void ClearHighlights()
    {
        foreach (GameObject line in highlights) if (line != null) Destroy(line);
        highlights.Clear();
    }
}
