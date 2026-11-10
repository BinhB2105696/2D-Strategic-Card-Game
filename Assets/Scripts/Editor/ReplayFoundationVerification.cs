#if UNITY_EDITOR || REPLAY_HEADLESS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

public static class ReplayFoundationVerification
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Card Game/Simulation/Verify Replay State and AI Contract")]
#endif
    public static void Verify()
    {
        var a = State(); var b = State();
        Equal(SimulationStateHash.Capture(a), SimulationStateHash.Capture(b), "same seed/state");
        string initial = SimulationStateHash.Capture(a);
        ulong count = a.Random.GeneratedValueCount;
        Equal(initial, SimulationStateHash.Capture(a), "capture is stable/read-only");
        Require(a.Random.GeneratedValueCount == count, "hash does not consume RNG");
        var previousCulture = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); Equal(initial, SimulationStateHash.Capture(a), "culture independence"); }
        finally { CultureInfo.CurrentCulture = previousCulture; }
        a.Players[0].TakeDamage(1);
        Different(initial, SimulationStateHash.Capture(a), "LP mutation");

        a = State(); CardInstance card = a.Players[0].Hand[0];
        card.ModifyAttack(-10000); string clamped = SimulationStateHash.Capture(a);
        card.ModifyAttack(-1);
        Require(card.CurrentAttack == 0, "ATK remains visibly clamped");
        Different(clamped, SimulationStateHash.Capture(a), "latent private ATK adjustment");
        a = State(); initial = SimulationStateHash.Capture(a);
        Field(a.Random, "state").SetValue(a.Random, 123456789u);
        Different(initial, SimulationStateHash.Capture(a), "private RNG state");

        a = State(); b = State();
        var usesA = (Dictionary<string, int>)Field(a, "effectUses").GetValue(a);
        var usesB = (Dictionary<string, int>)Field(b, "effectUses").GetValue(b);
        usesA.Add("effect_a", 1); usesA.Add("effect_b", 2);
        usesB.Add("effect_b", 2); usesB.Add("effect_a", 1);
        Equal(SimulationStateHash.Capture(a), SimulationStateHash.Capture(b), "dictionary insertion order ignored");
        var skipA = (HashSet<int>)Field(a.Players[0], "skippedBattleTurns").GetValue(a.Players[0]);
        var skipB = (HashSet<int>)Field(b.Players[0], "skippedBattleTurns").GetValue(b.Players[0]);
        skipA.Add(12); skipA.Add(14); skipB.Add(14); skipB.Add(12);
        Equal(SimulationStateHash.Capture(a), SimulationStateHash.Capture(b), "set insertion order ignored");
        initial = SimulationStateHash.Capture(a); skipA.Add(16);
        Different(initial, SimulationStateHash.Capture(a), "future skip schedule included");

        a = State(); initial = SimulationStateHash.Capture(a);
        ((List<StatModifierApplication>)Field(a, "statModifiers").GetValue(a)).Add(
            new StatModifierApplication(99, a.Players[0].Hand[0], 0, 0, 7));
        Different(initial, SimulationStateHash.Capture(a), "modifier lifetime record included even at equal visible stats");
        a = State();
        var chain = new ChainState(ResponseWindowType.AttackDeclaration, 0, new PendingAttack(0, 1, 2));
        typeof(GameState).GetProperty("ActiveChain").GetSetMethod(true).Invoke(a, new object[] { chain });
        initial = SimulationStateHash.Capture(a); chain.Pass();
        Different(initial, SimulationStateHash.Capture(a), "chain priority/pass count included");

        a = State(); int player = a.InteractionPlayerIndex;
        var none = new GameAction[0];
        string policy = SimulationJson.Encode(SimulationAiContract.Capture(a, player, none));
        initial = SimulationStateHash.Capture(a);
        ((List<CardInstance>)a.Players[1 - player].Deck).Reverse();
        Different(initial, SimulationStateHash.Capture(a), "hidden deck order included in diagnostic hash");
        Equal(policy, SimulationJson.Encode(SimulationAiContract.Capture(a, player, none)), "deck order excluded from policy");
        var hand = (List<CardInstance>)a.Players[1 - player].Hand;
        var deck = (List<CardInstance>)a.Players[1 - player].Deck;
        CardInstance hidden = hand[0]; hand[0] = deck[0]; deck[0] = hidden;
        hand[0].MoveToZone(CardZone.Hand, false); hidden.MoveToZone(CardZone.Deck, false);
        Equal(policy, SimulationJson.Encode(SimulationAiContract.Capture(a, player, none)), "opponent hand identities excluded");
        Require(!policy.Contains("InstanceId") && !policy.Contains("\"seed\"") && !policy.Contains("definitionsHash"), "policy omits diagnostic identities/metadata");
        var candidates = new[] { GameAction.AdvancePhase(player), GameAction.EnterEndPhase(player) };
        Require(SimulationAiContract.SelectedIndex(candidates, candidates[1]) == 1, "selected index points to original candidate");
        bool rejected = false;
        try { SimulationAiContract.SelectedIndex(candidates, GameAction.AdvancePhase(player)); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "reject an action outside the candidate list");
#if UNITY_EDITOR
        UnityEngine.Debug.Log("Replay foundation PASS: private state, RNG, ordering, culture, read-only capture and AI privacy/index contract.");
#else
        Console.WriteLine("Replay foundation PASS: private state, RNG, ordering, culture, read-only capture and AI privacy/index contract.");
#endif
    }

    private static GameState State()
    {
        var deck = Enumerable.Range(0, 30).Select(i => new CardDefinition("fixture_" + i, "fixture_" + i,
            CardCategory.Monster, "", MonsterAttribute.Wood, MonsterRank.Basic, MonsterType.Fairy,
            1000, 500 + i, 300, 1, SpellType.Normal, TrapType.Normal, 0, CardSpeed.Speed1)).ToList();
        return GameSession.Create(GameConfig.CreateDefault(), 42, deck, deck).State;
    }
    private static FieldInfo Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(value.GetType().Name, name);
    private static void Equal(string expected, string actual, string message) => Require(expected == actual, message);
    private static void Different(string first, string second, string message) => Require(first != second, message);
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Replay foundation: " + message); }
}
#endif
