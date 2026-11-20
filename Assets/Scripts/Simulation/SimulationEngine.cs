using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

public sealed class SimulationMatchResult
{
    public string matchId, pairId, bot0, bot1, status, error, actionDigest, resultDigest, finalStateHash, trace;
    public uint seed;
    public int scenario, deck0, deck1, leg, firstPlayer, winner = -2, turns, actions;
    public int[] decisions = new int[2], energySpent = new int[2], energyAllocated = new int[2], unusedAtTurnEnd = new int[2], finalLp = new int[2];
    public int slowDecisions;
    public int[] hardSearchedDecisions = new int[2], hardFallbackDecisions = new int[2], hardIterations = new int[2], hardRuleApplications = new int[2];
    public int[] hardChangedBaselineDecisions = new int[2];
    public int[] hardGuidedRootDecisions = new int[2];
    public int pairedRolloutDecisions;
    public int learnerRolloutComparisons;
    public int rolloutHorizonComparisons;
    public int behaviorSamples;
    public double[][] decisionTimesMs;
    public Dictionary<string, int> hardFallbackReasons = new Dictionary<string, int>();
    public double durationSeconds;
    public double[] meanDecisionMs = new double[2], medianDecisionMs = new double[2], p95DecisionMs = new double[2], maxDecisionMs = new double[2], preparationMs = new double[2], applyMs = new double[2];
    public Dictionary<string, long> eventCounts = new Dictionary<string, long>();
}

public static class SimulationEngine
{
    public static IBot Bot(string name, IReadOnlyList<CardDefinition> publicCatalog = null,
        IReadOnlyList<CardDefinition> knownOwnStartingDeck = null, LearnedPolicy learnedPolicy = null,
        uint explorationSeed = 0, bool lowerExploration = false)
    {
        switch (name)
        {
            case "Easy": return new EasyBot();
            case "Normal": return new NormalBot();
            case "Hard": return new HardBot(publicCatalog, knownOwnStartingDeck: knownOwnStartingDeck);
            case "SearchTeacher": return new HardBot(publicCatalog, iterations: 192, knownOwnStartingDeck: knownOwnStartingDeck);
            case "HardLow": return new HardBot(publicCatalog, knownOwnStartingDeck, null);
            case "LearnedSearch":
                if (learnedPolicy == null) throw new ArgumentNullException(nameof(learnedPolicy));
                return new HardBot(publicCatalog, knownOwnStartingDeck,
                    (state, player, legal) => learnedPolicy.Score(SimulationAiContract.Capture(state, player, legal)));
            case "Learned": return new LearnedBot(learnedPolicy);
            case "LearnedTrain": return new LearnedBot(learnedPolicy, new SeededGameRandom(explorationSeed), lowerExploration);
            default: throw new ArgumentException("Unsupported bot: " + name);
        }
    }

    private static double Ms(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

    public static SimulationMatchResult Run(SimulationConfig config, IReadOnlyList<CardDefinition> deck0,
        IReadOnlyList<CardDefinition> deck1, SimulationMatchResult result, string tracePath, bool detailed,
        Action<SimulationMatchResult> heartbeat = null, IReadOnlyList<CardDefinition> publicCatalog = null,
        LearnedPolicy learnedPolicy = null, uint explorationSeed = 0)
    {
        GameSession session = GameSession.Create(GameConfig.CreateDefault(), result.seed, deck0, deck1);
        result.seed = session.Log.Header.Seed;
        result.firstPlayer = session.State.CurrentPlayerIndex;
        IBot[] bots = { Bot(result.bot0, publicCatalog, deck0, learnedPolicy, explorationSeed, config.lowerLearnerExploration),
            Bot(result.bot1, publicCatalog, deck1, learnedPolicy, explorationSeed, config.lowerLearnerExploration) };
        if (detailed && config.collectPairedRolloutScores)
        {
            if (result.bot0 == "HardLow" && bots[0] is HardBot left) left.CollectPairedRolloutScores = true;
            if (result.bot1 == "HardLow" && bots[1] is HardBot right) right.CollectPairedRolloutScores = true;
        }
        var comparisons = new LearnedTeacherComparison[2];
        var rolloutComparisons = new HardBot[2];
        if (detailed && config.compareLearnedTeachers)
        {
            if (bots[0] is LearnedBot) comparisons[0] = new LearnedTeacherComparison(publicCatalog, deck0);
            if (bots[1] is LearnedBot) comparisons[1] = new LearnedTeacherComparison(publicCatalog, deck1);
        }
        if (detailed && config.compareLearnedRollouts)
        {
            if (bots[0] is LearnedBot) rolloutComparisons[0] = new HardBot(publicCatalog, deck0, null);
            if (bots[1] is LearnedBot) rolloutComparisons[1] = new HardBot(publicCatalog, deck1, null);
        }
        var times = new[] { new List<double>(), new List<double>() };
        var actionText = new StringBuilder();
        var watch = Stopwatch.StartNew(); double nextHeartbeat = 0;
        using (var trace = new StreamWriter(tracePath, false, new UTF8Encoding(false)) { AutoFlush = true })
        {
            trace.WriteLine(SimulationJson.Encode(SimulationJson.Object("record", "start", "matchId", result.matchId,
                "schema", SimulationReplayFormat.TraceSchema, "stateHashSchema", SimulationStateHash.Schema,
                "aiSchema", SimulationAiContract.Schema, "aiDescriptionHash", SimulationJson.Hash(SimulationJson.Encode(SimulationAiContract.Describe())),
                "seed", result.seed, "bot0", result.bot0, "bot1", result.bot1, "detailed", detailed,
                "botProfiles", bots.Select(bot => bot is HardBot hard ? (object)hard.Describe()
                    : bot is LearnedBot learned ? learned.Describe() : bot.DisplayName).ToArray(),
                "publicSearchCatalogHash", bots.Any(bot => bot is HardBot) ? SimulationReplayFormat.DefinitionsHash(publicCatalog) : null,
                "deck0", deck0.Select(card => card.CardId).ToArray(), "deck1", deck1.Select(card => card.CardId).ToArray(),
                "rulesHash", SimulationReplayFormat.RulesHash(), "definitionsHash", SimulationReplayFormat.DefinitionsHash(deck0.Concat(deck1)),
                "stateHash", SimulationStateHash.Capture(session.State))));
            try
            {
                while (!session.State.IsFinished)
                {
                    GameState state = session.State;
                    if (state.TurnNumber > config.maxTurns) { result.status = "TurnLimit"; break; }
                    if (result.actions >= config.maxActions) { result.status = "ActionLimit"; break; }
                    if (watch.Elapsed.TotalSeconds > config.maxMatchSeconds) { result.status = "TimeLimit"; break; }
                    if (watch.Elapsed.TotalSeconds >= nextHeartbeat)
                    {
                        nextHeartbeat = watch.Elapsed.TotalSeconds + 1;
                        result.turns = state.TurnNumber; heartbeat?.Invoke(result);
                    }
                    int player = state.InteractionPlayerIndex;
                    int turn = state.TurnNumber; string phase = state.CurrentPhase.ToString();
                    GameAction action = AutomaticAction(state);
                    bool automatic = action != null;
                    IReadOnlyList<GameAction> legal = null;
                    string observation = null;
                    double preparation = 0, decision = 0;
                    object hardSearch = null;
                    object teacherComparison = null;
                    object pairedRollout = null;
                    object learnerRollout = null;
                    if (!automatic)
                    {
                        long start = Stopwatch.GetTimestamp();
                        legal = GameRules.GetLegalActions(state, player);
                        if (legal.Count == 0) { result.status = "NoLegalActions"; throw new InvalidOperationException("No legal action at turn " + turn + ", " + phase); }
                        BotGameStateView view = BotGameStateView.Create(state, player);
                        if (bots[player] is IStateObservingBot observing) observing.Observe(state, player);
                        preparation = Ms(start); result.preparationMs[player] += preparation;
                        if (detailed) observation = SimulationJson.Encode(SimulationObservation.Capture(state, player));
                        start = Stopwatch.GetTimestamp(); action = bots[player].ChooseAction(view, legal);
                        decision = Ms(start); times[player].Add(decision); result.decisions[player]++;
                        if (bots[player] is HardBot hard)
                        {
                            result.hardIterations[player] += hard.LastIterations;
                            result.hardRuleApplications[player] += hard.LastRuleApplications;
                            if (hard.LastChangedBaseline) result.hardChangedBaselineDecisions[player]++;
                            if (hard.LastUsedLearnedRanking) result.hardGuidedRootDecisions[player]++;
                            if (hard.LastRolloutCandidates != null)
                            {
                                int[] indices = hard.LastRolloutCandidates.Select(candidate =>
                                    SimulationAiContract.SelectedIndex(legal, candidate)).ToArray();
                                pairedRollout = SimulationJson.Object("legalIndices", indices, "relativeScores", hard.LastPairedRolloutScores);
                                result.pairedRolloutDecisions++;
                            }
                            if (hard.LastIterations > 0 && hard.LastFallbackReason == null) result.hardSearchedDecisions[player]++;
                            if (hard.LastFallbackReason != null)
                            {
                                result.hardFallbackDecisions[player]++;
                                result.hardFallbackReasons.TryGetValue(hard.LastFallbackReason, out int count);
                                result.hardFallbackReasons[hard.LastFallbackReason] = count + 1;
                            }
                            hardSearch = SimulationJson.Object("iterations", hard.LastIterations, "nodes", hard.LastNodes,
                                "ruleApplications", hard.LastRuleApplications, "milliseconds", hard.LastSearchMilliseconds,
                                "fallback", hard.LastFallbackReason, "changedBaseline", hard.LastChangedBaseline);
                        }
                        if (decision > config.decisionWarningMs) result.slowDecisions++;
                        if (action == null || !legal.Any(candidate => ReferenceEquals(candidate, action)))
                        { result.status = "InvalidAction"; throw new InvalidOperationException("Bot must return an action from the supplied list."); }
                        teacherComparison = comparisons[player]?.Compare(state, view, legal);
                        var compared = rolloutComparisons[player]?.CompareLearnerAction(state, view, legal, action,
                            config.compareRolloutHorizons, config.focusTerminalRollouts);
                        if (compared.HasValue)
                        {
                            var sample = compared.Value;
                            int[] indices = sample.Candidates.Select(candidate => SimulationAiContract.SelectedIndex(legal, candidate)).ToArray();
                            var comparison = SimulationJson.Object("status", sample.Status, "tacticalIndex",
                                SimulationAiContract.SelectedIndex(legal, sample.Tactical), "hardLowIndex",
                                SimulationAiContract.SelectedIndex(legal, sample.Searched), "legalIndices", indices,
                                "relativeScores", sample.Scores);
                            if (sample.LongScores != null)
                            {
                                comparison.Add("longRelativeScores", sample.LongScores);
                                comparison.Add("longTerminalRewards", sample.TerminalRewards);
                                result.rolloutHorizonComparisons++;
                            }
                            learnerRollout = comparison;
                            result.learnerRolloutComparisons++;
                            if (sample.Scores != null) result.pairedRolloutDecisions++;
                        }
                    }
                    string encodedAction = SimulationJson.Encode(SimulationJson.Action(action));
                    string policyInput = detailed && !automatic
                        ? SimulationJson.Encode(SimulationAiContract.Capture(state, player, legal)) : null;
                    // Attempt is flushed before applying; crashes/rejections retain the proposal.
                    var attempt = SimulationJson.Object("record", "attempt", "sequence", result.actions + 1,
                        "turn", turn, "phase", phase, "automatic", automatic, "action", SimulationJson.Action(action),
                        "observationJson", observation, "legalActions", detailed && legal != null ? legal.Select(SimulationJson.Action).ToArray() : null,
                        "stateHash", SimulationStateHash.Capture(state), "policyInputJson", policyInput,
                        "policyHash", policyInput == null ? null : SimulationJson.Hash(policyInput),
                        "selectedActionIndex", automatic ? -1 : SimulationAiContract.SelectedIndex(legal, action),
                        "preparationMs", preparation, "decisionMs", decision, "hardSearch", hardSearch);
                    if (teacherComparison != null) attempt.Add("teacherComparison", teacherComparison);
                    if (pairedRollout != null) attempt.Add("pairedRollout", pairedRollout);
                    if (learnerRollout != null) attempt.Add("learnerRolloutComparison", learnerRollout);
                    if (!automatic && bots[player] is LearnedBot learner && learner.LastBehaviorLogProbability.HasValue)
                    {
                        attempt.Add("behaviorLogProbability", learner.LastBehaviorLogProbability.Value);
                        result.behaviorSamples++;
                    }
                    trace.WriteLine(SimulationJson.Encode(attempt));
                    int unused = state.Players[player].AvailableEnergy;
                    long applyStart = Stopwatch.GetTimestamp();
                    ActionResult applied = session.TryApply(action);
                    result.applyMs[player] += Ms(applyStart);
                    if (!applied.Succeeded) { result.status = "RuleRejected"; throw new InvalidOperationException(applied.ErrorMessage); }
                    result.actions++;
                    actionText.Append(encodedAction).Append('\n');
                    if (action.Type == GameActionType.EndTurn) result.unusedAtTurnEnd[player] += unused;
                    foreach (GameEvent entry in applied.Events)
                    {
                        string type = entry.Type.ToString();
                        result.eventCounts.TryGetValue(type, out long count); result.eventCounts[type] = count + 1;
                        if (entry.PlayerIndex < 0 || entry.PlayerIndex > 1) continue;
                        if (entry.Type == GameEventType.EnergySpent) result.energySpent[entry.PlayerIndex] += entry.Amount;
                        if (entry.Type == GameEventType.EnergyAllocated) result.energyAllocated[entry.PlayerIndex] += entry.Amount;
                    }
                    trace.WriteLine(SimulationJson.Encode(SimulationJson.Object("record", "accepted", "sequence", result.actions,
                        "events", applied.Events, "terminated", state.IsFinished,
                        "stateHash", SimulationStateHash.Capture(state),
                        "eventsHash", SimulationJson.Hash(SimulationJson.Encode(applied.Events)),
                        "reward0", state.IsFinished ? Reward(state.WinnerPlayerIndex, 0) : 0,
                        "reward1", state.IsFinished ? Reward(state.WinnerPlayerIndex, 1) : 0)));
                }
                if (session.State.IsFinished) { result.status = "Finished"; result.winner = session.State.WinnerPlayerIndex; }
            }
            catch (IOException) { throw; }
            catch (UnauthorizedAccessException) { throw; }
            catch (Exception exception)
            {
                if (string.IsNullOrEmpty(result.status)) result.status = "Exception";
                result.error = exception.ToString();
            }
            result.turns = session.State.TurnNumber;
            result.finalLp = session.State.Players.Select(player => player.Health).ToArray();
            result.finalStateHash = SimulationStateHash.Capture(session.State);
            result.actionDigest = SimulationJson.Hash(actionText.ToString());
            result.resultDigest = SimulationJson.Hash(SimulationJson.Encode(SimulationJson.Object("status", result.status,
                "winner", result.winner, "turns", result.turns, "lp", result.finalLp, "actions", result.actions,
                "actionDigest", result.actionDigest)));
            result.durationSeconds = watch.Elapsed.TotalSeconds;
            for (int player = 0; player < 2; player++)
            {
                times[player].Sort();
                if (times[player].Count == 0) continue;
                result.meanDecisionMs[player] = times[player].Average();
                result.medianDecisionMs[player] = Percentile(times[player], 0.5);
                result.p95DecisionMs[player] = Percentile(times[player], 0.95);
                result.maxDecisionMs[player] = times[player][times[player].Count - 1];
            }
            if (config.recordDecisionTimes) result.decisionTimesMs = times.Select(values => values.ToArray()).ToArray();
            if (detailed || result.status != "Finished") result.trace = Path.GetFileName(tracePath);
            trace.WriteLine(SimulationJson.Encode(SimulationJson.Object("record", "end", "result", result,
                "stateHash", result.finalStateHash,
                "terminated", result.status == "Finished", "truncated", IsTruncated(result.status),
                "reward0", result.status == "Finished" ? Reward(result.winner, 0) : 0,
                "reward1", result.status == "Finished" ? Reward(result.winner, 1) : 0)));
        }
        if (result.trace == null) File.Delete(tracePath); // Only this match's generated spool, never user data.
        return result;
    }

    public static bool IsTruncated(string status) => status == "TurnLimit" || status == "ActionLimit" || status == "TimeLimit";
    public static GameAction AutomaticAction(GameState state)
    {
        if (state.IsFinished || state.HasOpenResponseWindow || state.HasPendingTriggeredEffect) return null;
        int player = state.InteractionPlayerIndex;
        if (state.CurrentPhase == TurnPhase.Standby) return GameAction.AdvancePhase(player);
        if (state.CurrentPhase != TurnPhase.Recovery) return null;
        return state.CurrentRecoveryStep == RecoveryStep.Draw
            ? GameAction.ResolveDrawStep(player) : GameAction.ResolveEnergyRefreshStep(player);
    }
    private static int Reward(int winner, int player) => winner < 0 ? 0 : winner == player ? 1 : -1;
    private static double Percentile(List<double> sorted, double p) => sorted[Math.Max(0, (int)Math.Ceiling(sorted.Count * p) - 1)];
}
