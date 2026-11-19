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
        VerifyDeckExhaustionEndTurnRule(deck);
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
        File.WriteAllText(Path.Combine(directory, "PASS.txt"), "PASS: reproducible actions/results, first-turn draw rule, escalating end-turn deck exhaustion, termination vs limits, hidden information, D-drive containment, paired aggregation, trace metadata and storage failure propagation.\n");
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

    private static void VerifyDeckExhaustionEndTurnRule(IReadOnlyList<CardDefinition> deck)
    {
        var config = new GameConfig(250, 30, 40, 30, 1, 5, 1, 3, 3, 500, 100);
        GameSession session = GameSession.Create(config, 44, deck, deck);
        int first = session.State.CurrentPlayerIndex, second = 1 - first;
        ActionResult firstDraw = Apply(session, GameAction.ResolveDrawStep(first));
        Require(session.State.Players[first].DeckExhaustionLevel == 0 &&
            session.State.Players[first].PendingDeckExhaustionDamage == 0 &&
            !firstDraw.Events.Any(item => item.Type == GameEventType.DeckExhaustionDamageScheduled),
            "turn-one skipped draw does not schedule deck exhaustion");
        Require(session.State.Players[first].DrawCards(2) == 0 &&
            session.State.Players[first].DeckExhaustionLevel == 0,
            "effect-style empty draw does not schedule Draw-step exhaustion");
        AdvanceToEnd(session, first);
        Apply(session, GameAction.EndTurn(first));

        ActionResult scheduled = Apply(session, GameAction.ResolveDrawStep(second));
        Require(session.State.Players[second].Health == 250 &&
            session.State.Players[second].DeckExhaustionLevel == 1 &&
            session.State.Players[second].PendingDeckExhaustionDamage == 100 &&
            scheduled.Events.Any(item => item.Type == GameEventType.DeckExhaustionDamageScheduled && item.Amount == 100),
            "first failed normal Draw step schedules 100 damage without applying it immediately");
        AdvanceToEnd(session, second);
        ActionResult firstPenalty = Apply(session, GameAction.EndTurn(second));
        Require(session.State.Players[second].Health == 150 &&
            session.State.Players[second].PendingDeckExhaustionDamage == 0 &&
            firstPenalty.Events.Any(item => item.Type == GameEventType.PlayerDamaged &&
                item.Amount == 100 && item.Phase == TurnPhase.End),
            "scheduled deck exhaustion damage applies only at EndTurn");

        Apply(session, GameAction.ResolveDrawStep(first));
        AdvanceToEnd(session, first);
        Apply(session, GameAction.EndTurn(first));
        ActionResult secondFailure = Apply(session, GameAction.ResolveDrawStep(second));
        Require(session.State.Players[second].Health == 150 &&
            session.State.Players[second].DeckExhaustionLevel == 2 &&
            session.State.Players[second].PendingDeckExhaustionDamage == 200 &&
            secondFailure.Events.Any(item => item.Type == GameEventType.DeckExhaustionDamageScheduled && item.Amount == 200),
            "second failed Draw step schedules level-times-100 damage");
        AdvanceToEnd(session, second);
        ActionResult lethal = Apply(session, GameAction.EndTurn(second));
        Require(session.State.IsFinished && session.State.WinnerPlayerIndex == first &&
            session.State.Players[second].Health == 0 &&
            lethal.Events.Any(item => item.Type == GameEventType.MatchEnded) &&
            !lethal.Events.Any(item => item.Type == GameEventType.TurnStarted),
            "lethal exhaustion ends the match at EndTurn before the opponent's turn starts");

        GameSession skipped = GameSession.Create(config, 45, deck, deck);
        first = skipped.State.CurrentPlayerIndex; second = 1 - first;
        Apply(skipped, GameAction.ResolveDrawStep(first));
        AdvanceToEnd(skipped, first);
        Apply(skipped, GameAction.EndTurn(first));
        skipped.State.Players[second].ScheduleDrawStepSkip(skipped.State.TurnNumber);
        ActionResult skippedDraw = Apply(skipped, GameAction.ResolveDrawStep(second));
        Require(skipped.State.Players[second].DeckExhaustionLevel == 0 &&
            skipped.State.Players[second].PendingDeckExhaustionDamage == 0 &&
            !skippedDraw.Events.Any(item => item.Type == GameEventType.DeckExhaustionDamageScheduled),
            "effect-skipped Draw step does not schedule deck exhaustion");
    }

    private static void AdvanceToEnd(GameSession session, int player)
    {
        if (session.State.CurrentPhase == TurnPhase.Recovery &&
            session.State.CurrentRecoveryStep == RecoveryStep.EnergyRefresh)
            Apply(session, GameAction.ResolveEnergyRefreshStep(player));
        if (session.State.CurrentPhase == TurnPhase.Standby)
            Apply(session, GameAction.AdvancePhase(player));
        while (session.State.CurrentPhase != TurnPhase.End)
            Apply(session, GameAction.AdvancePhase(player));
    }

    private static ActionResult Apply(GameSession session, GameAction action)
    {
        ActionResult result = session.TryApply(action);
        Require(result.Succeeded, "draw-rule fixture action succeeds: " + result.ErrorMessage);
        return result;
    }
    private static List<CardDefinition> Deck(string prefix) => Enumerable.Range(0, 30).Select(i => new CardDefinition(
        prefix + i, prefix + i, CardCategory.Monster, "", MonsterAttribute.Wood, MonsterRank.Basic, MonsterType.Fairy,
        1000, 500 + i % 4 * 300, 300, 1, SpellType.Normal, TrapType.Normal, 0, CardSpeed.Speed1)).ToList();
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Simulation verification: " + message); }
}
