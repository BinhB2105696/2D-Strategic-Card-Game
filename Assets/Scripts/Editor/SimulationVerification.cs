using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SimulationVerification
{
    [MenuItem("Card Game/Simulation/Verify Runner")]
    public static void Verify()
    {
        string root = SimulationJson.ResultsRoot(Path.GetFullPath("."));
        string directory = Path.Combine(root, "verification_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(directory);
        var config = new SimulationConfig { projectRoot = Path.GetFullPath("."), warmupMatches = 0 };
        var deck = Deck("known");
        SimulationMatchResult a = Match("repeat_a"), b = Match("repeat_b");
        SimulationEngine.Run(config, deck, deck, a, Path.Combine(directory, "a.jsonl"), true);
        SimulationEngine.Run(config, deck, deck, b, Path.Combine(directory, "b.jsonl"), true);
        Require(a.status == "Finished" && b.status == "Finished", "matches finish");
        Require(a.actionDigest == b.actionDigest && a.resultDigest == b.resultDigest, "same seed/decks/policies reproduce actions and outcomes");
        Require(File.ReadLines(Path.Combine(directory, "a.jsonl")).Last().Contains("\"trace\":\"a.jsonl\""), "trace end record names its retained file");
        VerifyFirstTurnDrawRule(deck);
        bool storageFailureEscaped = false;
        try
        {
            SimulationEngine.Run(config, deck, deck, Match("storage_failure"), Path.Combine(directory, "storage_failure.jsonl"), false,
                result => { throw new UnauthorizedAccessException("Injected storage failure"); });
        }
        catch (UnauthorizedAccessException) { storageFailureEscaped = true; }
        Require(storageFailureEscaped, "storage permission failure is reported at batch level");
        config.maxActions = 1;
        SimulationMatchResult limited = Match("limited");
        SimulationEngine.Run(config, deck, deck, limited, Path.Combine(directory, "limit.jsonl"), false);
        Require(limited.status == "ActionLimit" && limited.winner == -2 && File.Exists(Path.Combine(directory, "limit.jsonl")), "limit is not draw/loss and keeps trace");
        GameState state = GameState.CreateNewMatch(GameConfig.CreateDefault(), 3, deck, Deck("SECRET_OPPONENT"));
        Require(!SimulationJson.Encode(SimulationObservation.Capture(state, 0)).Contains("SECRET_OPPONENT"), "opponent hand/deck is not policy input");
        bool rejected = false;
        try { SimulationJson.ResultsRoot(@"C:\not-the-project"); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "reject C-drive results");
        var matchup = new SimulationMatchup { player0Bot = "Normal", player1Bot = "Easy", swapBots = true };
        config.matchups = new[] { matchup };
        a.pairId = b.pairId = "complete"; a.winner = 0; b.bot0 = "Easy"; b.bot1 = "Normal"; b.winner = 1;
        limited.pairId = "incomplete";
        string summary = SimulationJson.Encode(SimulationSummary.Create(new List<SimulationMatchResult> { a, b, limited }, config));
        Require(summary.Contains("\"completeSeedPairs\":1") && summary.Contains("\"pairedScoreRate\":1"), "incomplete pairs excluded from paired score");
        config.matchups = new[] { new SimulationMatchup { player0Bot = "Easy", player1Bot = "Easy", swapBots = false } };
        var selfPlay = new SimulationMatchResult { status = "Finished", pairId = "self", bot0 = "Easy", bot1 = "Easy", winner = 0 };
        string selfSummary = SimulationJson.Encode(SimulationSummary.Create(new List<SimulationMatchResult> { selfPlay }, config, false));
        Require(selfSummary.Contains("\"completeSeedPairs\":1") && selfSummary.Contains("\"pairedScore95Interval\":null"), "self-play pairs counted and interim confidence calculation skipped");
        File.WriteAllText(Path.Combine(directory, "PASS.txt"), "PASS: reproducible actions/results, first-turn draw rule, termination vs limits, hidden information, D-drive containment, paired aggregation, trace metadata and storage failure propagation.\n");
        Debug.Log("Simulation verification PASS: " + directory);
    }

    private static SimulationMatchResult Match(string id) => new SimulationMatchResult { matchId = id, pairId = id, seed = 42, bot0 = "Normal", bot1 = "Easy" };
    private static void VerifyFirstTurnDrawRule(IReadOnlyList<CardDefinition> deck)
    {
        GameSession session = GameSession.Create(GameConfig.CreateDefault(), 43, deck, deck);
        int first = session.State.CurrentPlayerIndex, second = 1 - first;
        Require(session.State.Players[first].Hand.Count == 4 && session.State.Players[second].Hand.Count == 4, "both players start with four cards");
        Apply(session, GameAction.ResolveDrawStep(first));
        Require(session.State.Players[first].Hand.Count == 4 && session.State.CurrentRecoveryStep == RecoveryStep.EnergyRefresh,
            "first player's first Draw step advances without drawing");
        Apply(session, GameAction.ResolveEnergyRefreshStep(first));
        if (session.State.CurrentPhase == TurnPhase.Standby) Apply(session, GameAction.AdvancePhase(first));
        Apply(session, GameAction.AdvancePhase(first));
        Apply(session, GameAction.EndTurn(first));
        Apply(session, GameAction.ResolveDrawStep(second));
        Require(session.State.Players[second].Hand.Count == 5, "second player draws normally on turn two");
        Apply(session, GameAction.ResolveEnergyRefreshStep(second));
        if (session.State.CurrentPhase == TurnPhase.Standby) Apply(session, GameAction.AdvancePhase(second));
        Apply(session, GameAction.AdvancePhase(second));
        Apply(session, GameAction.AdvancePhase(second));
        Apply(session, GameAction.EndTurn(second));
        Apply(session, GameAction.ResolveDrawStep(first));
        Require(session.State.Players[first].Hand.Count == 5, "first player draws normally on their next turn");
    }
    private static void Apply(GameSession session, GameAction action)
    {
        ActionResult result = session.TryApply(action);
        Require(result.Succeeded, "draw-rule fixture action succeeds: " + result.ErrorMessage);
    }
    private static List<CardDefinition> Deck(string prefix) => Enumerable.Range(0, 30).Select(i => new CardDefinition(
        prefix + i, prefix + i, CardCategory.Monster, "", MonsterAttribute.Wood, MonsterRank.Basic, MonsterType.Fairy,
        1000, 500 + i % 4 * 300, 300, 1, SpellType.Normal, TrapType.Normal, 0, CardSpeed.Speed1)).ToList();
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Simulation verification: " + message); }
}
