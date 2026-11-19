#if UNITY_EDITOR || REPLAY_HEADLESS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class SimulationExpansionAiVerification
{
    public static void Verify()
    {
        var cards = StrategicCardExpansionCatalog.Create().ToDictionary(card => card.CardId);
        var deck = Enumerable.Repeat(cards["bloodfang_raider"], 30).ToArray();
        GameState state = GameState.CreateNewMatch(GameConfig.CreateDefault(), 77, deck, deck);
        Call(state, "StartNextTurn"); Call(state, "SetCurrentPhase", TurnPhase.Main);
        int actor = state.CurrentPlayerIndex, enemy = 1 - actor;
        var source = Place(state, actor, 0, 900001, cards["scar_duelist"]);
        var target = Place(state, enemy, 0, 900002, cards["aegis_sentinel"]);
        state.Players[actor].TakeDamage(1600);
        Call(state, "CaptureMainStart");
        var policy = Capture(state, actor);
        var global = (double[])policy["globalFeatures"];
        Check(global.Length == 25 && global[17] == 0.2 && global[18] == 0.2, "start-of-Main monster counts");
        double startLp = global[19];
        state.Players[actor].TakeDamage(100);
        Place(state, actor, 1, 900003, cards["battle_herald"]);
        global = (double[])Capture(state, actor)["globalFeatures"];
        Check(global[17] == 0.2 && global[19] == startLp && global[6] != startLp, "snapshot survives later LP/board changes");
        var opposite = (double[])Capture(state, enemy)["globalFeatures"];
        Check(opposite[19] == global[20] && opposite[20] == global[19], "snapshot uses viewer-relative owners");

        string before = SimulationJson.Encode(Capture(state, actor));
        Call(state, "AddRetaliationMark", source, target, 300);
        string stateHash = SimulationStateHash.Capture(state);
        policy = Capture(state, actor);
        Check(before != SimulationJson.Encode(policy), "public mark changes policy even at equal visible stats");
        Check(stateHash == SimulationStateHash.Capture(state), "capture does not consume/clean marks or mutate state/RNG");
        var marks = (List<object>)policy["retaliationMarks"];
        Check(marks.Count == 1, "active public mark included once");
        var mark = (Dictionary<string, object>)marks[0];
        var entities = (List<object>)policy["entities"];
        Check((string)((Dictionary<string, object>)entities[(int)mark["source"]])["cardId"] == "scar_duelist" &&
            (string)((Dictionary<string, object>)entities[(int)mark["target"]])["cardId"] == "aegis_sentinel", "mark uses local source/target indices");
        Check(((double[])mark["features"])[0] == 0.3, "mark damage reduction scale");
        Check(!SimulationJson.Encode(policy).Contains("900001") && !SimulationJson.Encode(policy).Contains("900002"), "raw instance IDs excluded");
        // Privacy fixture only: do not invoke the Spell/Trap Set action on a monster.
        target.MoveToZone(CardZone.MonsterZone, true);
        Check(((List<object>)Capture(state, actor)["retaliationMarks"]).Count == 0, "hidden endpoint does not reveal a relationship");
        target.MoveToZone(CardZone.MonsterZone, false);
        Check(((List<object>)Capture(state, actor)["retaliationMarks"]).Count == 1, "hiding an endpoint does not consume its mark");
        Call(state, "CleanRetaliationMarks", state.TurnNumber + 1);
        Check(((List<object>)Capture(state, actor)["retaliationMarks"]).Count == 0, "expired mark removed");

        // The new search/recovery choices must resolve to known definitions, not opaque IDs.
        var archivist = Place(state, actor, 2, 900004, cards["arcane_archivist"]);
        state.Players[actor].RestoreEnergy(5);
        var mutableDeck = (List<CardInstance>)typeof(PlayerState).GetProperty("MutableDeck", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state.Players[actor]);
        var choice = new CardInstance(900005, actor, cards["stop_defense"]);
        choice.MoveToZone(CardZone.Deck, false); mutableDeck.Add(choice);
        var legal = GameRules.GetLegalActions(state, actor);
        Check(legal.Any(action => action.SourceInstanceId == archivist.InstanceId && action.TargetInstanceIds.Contains(choice.InstanceId)), "new deck search is a legal candidate");
        policy = (Dictionary<string, object>)SimulationAiContract.Capture(state, actor, legal);
        entities = (List<object>)policy["entities"];
        Check(entities.Cast<Dictionary<string, object>>().Any(entity => (string)entity["cardId"] == "stop_defense"), "legal own-deck search choice is encoded");
        Check(SimulationAiContract.Schema == 3 &&
            ((double[])Capture(state, actor)["globalFeatures"]).Length == 25,
            "deck-exhaustion observations are explicitly versioned");
    }

    private static Dictionary<string, object> Capture(GameState state, int player)
        => (Dictionary<string, object>)SimulationAiContract.Capture(state, player, new GameAction[0]);

    private static CardInstance Place(GameState state, int owner, int slot, long id, CardDefinition definition)
    {
        var card = new CardInstance(id, owner, definition);
        Call(state.Players[owner], "PlaceMonsterFromHand", card, slot, BattlePosition.Attack);
        card.ResetTurnFlags();
        return card;
    }

    private static void Call(object target, string name, params object[] arguments)
        => (target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().Name, name)).Invoke(target, arguments);

    private static void Check(bool passed, string message)
    { if (!passed) throw new InvalidOperationException("Expansion AI contract: " + message); }
}
#endif
