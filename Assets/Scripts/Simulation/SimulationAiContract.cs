using System;
using System.Collections.Generic;
using System.Linq;

public static class SimulationAiContract
{
    public const int Schema = 1;
    public static object Describe() => SimulationJson.Object("schema", Schema,
        "semantics", "per-player/next-decision transitions; terminal rewards include intervening opponent and automatic steps",
        "globalFeatures", new[] { "turn/100", "phase", "ownTurn", "ownPriority", "response", "trigger",
            "ownLP/1000", "enemyLP/1000", "ownEnergy/5", "enemyEnergy/5", "ownMaxEnergy/5", "enemyMaxEnergy/5",
            "ownDeck/40", "enemyDeck/40", "ownHand/10", "enemyHand/10", "finished" },
        "entityFeatures", new[] { "relativeOwner", "zone", "slot", "known", "category", "rank", "position",
            "hp/1000", "maxHP/1000", "atk/1000", "def/1000", "energy/5", "attackEnergy/5", "tributes/3",
            "speed", "remainingAttacks", "changedPosition", "summonedThisTurn" },
        "actionTypes", EnumValues(typeof(GameActionType)), "positions", EnumValues(typeof(BattlePosition)),
        "zones", EnumValues(typeof(CardZone)), "phases", EnumValues(typeof(TurnPhase)),
        "categories", EnumValues(typeof(CardCategory)), "ranks", EnumValues(typeof(MonsterRank)), "speeds", EnumValues(typeof(CardSpeed)),
        "actionFeatures", new[] { "type", "zone", "amount/5", "position" },
        "reward", "ongoing/draw=0, win=1, loss=-1; limits are truncated, never losses",
        "identity", "cardId is a known categorical label; source/target/tribute indices address local entities, never raw instance IDs",
        "mask", "only legal candidates are present; each mask entry is 1",
        "categoricalVocabulary", "Use a fixed public card catalog, not per-match deck metadata, for cardId embeddings.");

    private static object[] EnumValues(Type type) => Enum.GetValues(type).Cast<object>()
        .Select(value => (object)SimulationJson.Object("name", value.ToString(), "value", Convert.ToInt32(value))).ToArray();

    public static int SelectedIndex(IReadOnlyList<GameAction> legal, GameAction selected)
    {
        for (int i = 0; i < legal.Count; i++) if (ReferenceEquals(legal[i], selected)) return i;
        throw new InvalidOperationException("Selected action must belong to the supplied legal list.");
    }

    public static object Capture(GameState state, int player, IReadOnlyList<GameAction> legal)
    {
        if (player < 0 || player > 1) throw new ArgumentOutOfRangeException(nameof(player));
        if (legal == null) throw new ArgumentNullException(nameof(legal));
        if (legal.Count > 0 && (state.IsFinished || state.InteractionPlayerIndex != player))
            throw new InvalidOperationException("Legal candidates require the acting player.");
        BotGameStateView view = BotGameStateView.Create(state, player);
        BotPlayerView own = view.Players[player], enemy = view.Players[1 - player];
        var entities = new List<object>();
        var indices = new Dictionary<long, int>();
        Action<BotCardView, int> add = (card, slot) =>
        {
            if (card == null || indices.ContainsKey(card.InstanceId)) return;
            bool known = card.IsKnown;
            CardInstance flags = known ? state.FindCard(card.InstanceId) : null;
            indices.Add(card.InstanceId, entities.Count);
            entities.Add(SimulationJson.Object("cardId", known ? card.CardId : "", "features", new double[] {
                card.OwnerPlayerIndex == player ? 0 : 1, (int)card.Zone, slot, known ? 1 : 0,
                known ? (int)card.Category : -1, known ? (int)card.MonsterRank : -1, (int)card.BattlePosition,
                card.CurrentHealth/1000.0, card.MaximumHealth/1000.0, card.CurrentAttack/1000.0, card.CurrentDefense/1000.0,
                card.AllocatedEnergy/5.0, card.AttackEnergyRequirement/5.0, card.TributeCount/3.0,
                known ? (int)card.Speed : -1, flags == null ? 0 : Math.Max(0, flags.MaxAttacksThisTurn-flags.AttacksUsedThisTurn),
                flags != null && flags.HasChangedPositionThisTurn ? 1 : 0, flags != null && flags.WasSummonedThisTurn ? 1 : 0 }));
        };
        for (int relative = 0; relative < 2; relative++)
        {
            int owner = relative == 0 ? player : 1 - player;
            BotPlayerView visible = view.Players[owner];
            if (owner == player) for (int i = 0; i < visible.Hand.Count; i++) add(visible.Hand[i], i);
            for (int i = 0; i < visible.MonsterZones.Count; i++) add(visible.MonsterZones[i], i);
            for (int i = 0; i < visible.SpellTrapZones.Count; i++) add(visible.SpellTrapZones[i], i);
            add(visible.FieldSpell, 0);
            for (int i = 0; i < state.Players[owner].Graveyard.Count; i++)
                add(new BotCardView(state.Players[owner].Graveyard[i]), i);
        }
        Func<long, int> index = id =>
        {
            if (id == 0) return -1;
            if (indices.TryGetValue(id, out int found)) return found;
            // An opaque legal choice (e.g. a pending selection) reveals no definition,
            // raw ID or deck order. Richer search-selection views need a new contract.
            int opaque = entities.Count; indices.Add(id, opaque);
            var features = new double[18]; features[0] = features[1] = features[2] = -1;
            features[4] = features[5] = features[14] = -1;
            entities.Add(SimulationJson.Object("cardId", "", "features", features));
            return opaque;
        };
        var candidates = new List<object>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (GameAction action in legal)
        {
            if (action.PlayerIndex != player || !keys.Add(SimulationJson.Encode(SimulationJson.Action(action))))
                throw new InvalidOperationException("Duplicate or wrong-player legal candidate.");
            candidates.Add(SimulationJson.Object("features", new double[] { (int)action.Type, action.ZoneIndex,
                action.Amount/5.0, (int)action.BattlePosition }, "source", index(action.SourceInstanceId),
                "targets", action.TargetInstanceIds.Select(index).ToArray(),
                "tributes", action.TributeInstanceIds.Select(index).ToArray()));
        }
        return SimulationJson.Object("schema", Schema, "globalFeatures", new double[] {
            state.TurnNumber/100.0, (int)state.CurrentPhase, state.CurrentPlayerIndex == player ? 1 : 0,
            state.InteractionPlayerIndex == player ? 1 : 0, state.HasOpenResponseWindow ? 1 : 0,
            state.HasPendingTriggeredEffect ? 1 : 0, own.Health/1000.0, enemy.Health/1000.0,
            own.AvailableEnergy/5.0, enemy.AvailableEnergy/5.0, own.MaxEnergy/5.0, enemy.MaxEnergy/5.0,
            own.DeckCount/40.0, enemy.DeckCount/40.0, own.HandCount/10.0, enemy.HandCount/10.0, state.IsFinished ? 1 : 0 },
            "entities", entities, "legalActions", candidates, "legalActionMask", Enumerable.Repeat(1, legal.Count).ToArray());
    }

    public static int Reward(GameState state, int player) => !state.IsFinished || state.WinnerPlayerIndex < 0
        ? 0 : state.WinnerPlayerIndex == player ? 1 : -1;
}
