using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

public sealed class SimulationMatchResult
{
    public string matchId, pairId, bot0, bot1, status, error, actionDigest, resultDigest, trace;
    public uint seed;
    public int scenario, deck0, deck1, leg, firstPlayer, winner = -2, turns, actions;
    public int[] decisions = new int[2], energySpent = new int[2], energyAllocated = new int[2], unusedAtTurnEnd = new int[2], finalLp = new int[2];
    public int slowDecisions;
    public double durationSeconds;
    public double[] meanDecisionMs = new double[2], medianDecisionMs = new double[2], p95DecisionMs = new double[2], maxDecisionMs = new double[2], preparationMs = new double[2], applyMs = new double[2];
    public Dictionary<string, long> eventCounts = new Dictionary<string, long>();
}

public static class SimulationEngine
{
    public static IBot Bot(string name)
    {
        switch (name)
        {
            case "Easy": return new EasyBot();
            case "Normal": return new NormalBot();
            default: throw new ArgumentException("Unsupported bot: " + name);
        }
    }

    private static double Ms(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

    public static SimulationMatchResult Run(SimulationConfig config, IReadOnlyList<CardDefinition> deck0,
        IReadOnlyList<CardDefinition> deck1, SimulationMatchResult result, string tracePath, bool detailed,
        Action<SimulationMatchResult> heartbeat = null)
    {
        GameSession session = GameSession.Create(GameConfig.CreateDefault(), result.seed, deck0, deck1);
        result.seed = session.Log.Header.Seed;
        result.firstPlayer = session.State.CurrentPlayerIndex;
        IBot[] bots = { Bot(result.bot0), Bot(result.bot1) };
        var times = new[] { new List<double>(), new List<double>() };
        var actionText = new StringBuilder();
        var watch = Stopwatch.StartNew(); double nextHeartbeat = 0;
        using (var trace = new StreamWriter(tracePath, false, new UTF8Encoding(false)) { AutoFlush = true })
        {
            trace.WriteLine(SimulationJson.Encode(SimulationJson.Object("record", "start", "matchId", result.matchId,
                "seed", result.seed, "bot0", result.bot0, "bot1", result.bot1, "detailed", detailed)));
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
                    bool automatic = !state.HasOpenResponseWindow && !state.HasPendingTriggeredEffect &&
                        (state.CurrentPhase == TurnPhase.Recovery || state.CurrentPhase == TurnPhase.Standby);
                    GameAction action;
                    IReadOnlyList<GameAction> legal = null;
                    string observation = null;
                    double preparation = 0, decision = 0;
                    if (automatic)
                        action = state.CurrentPhase == TurnPhase.Standby ? GameAction.AdvancePhase(player) :
                            state.CurrentRecoveryStep == RecoveryStep.Draw ? GameAction.ResolveDrawStep(player) : GameAction.ResolveEnergyRefreshStep(player);
                    else
                    {
                        long start = Stopwatch.GetTimestamp();
                        legal = GameRules.GetLegalActions(state, player);
                        if (legal.Count == 0) { result.status = "NoLegalActions"; throw new InvalidOperationException("No legal action at turn " + turn + ", " + phase); }
                        BotGameStateView view = BotGameStateView.Create(state, player);
                        if (bots[player] is NormalBot normal) normal.Observe(state, player);
                        preparation = Ms(start); result.preparationMs[player] += preparation;
                        if (detailed) observation = SimulationJson.Encode(SimulationObservation.Capture(state, player));
                        start = Stopwatch.GetTimestamp(); action = bots[player].ChooseAction(view, legal);
                        decision = Ms(start); times[player].Add(decision); result.decisions[player]++;
                        if (decision > config.decisionWarningMs) result.slowDecisions++;
                        if (action == null || !legal.Any(candidate => ReferenceEquals(candidate, action)))
                        { result.status = "InvalidAction"; throw new InvalidOperationException("Bot must return an action from the supplied list."); }
                    }
                    string encodedAction = SimulationJson.Encode(SimulationJson.Action(action));
                    // Attempt is flushed before applying; crashes/rejections retain the proposal.
                    trace.WriteLine(SimulationJson.Encode(SimulationJson.Object("record", "attempt", "sequence", result.actions + 1,
                        "turn", turn, "phase", phase, "automatic", automatic, "action", SimulationJson.Action(action),
                        "observationJson", observation, "legalActions", detailed && legal != null ? legal.Select(SimulationJson.Action).ToArray() : null,
                        "preparationMs", preparation, "decisionMs", decision)));
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
            if (detailed || result.status != "Finished") result.trace = Path.GetFileName(tracePath);
            trace.WriteLine(SimulationJson.Encode(SimulationJson.Object("record", "end", "result", result,
                "terminated", result.status == "Finished", "truncated", IsTruncated(result.status),
                "reward0", result.status == "Finished" ? Reward(result.winner, 0) : 0,
                "reward1", result.status == "Finished" ? Reward(result.winner, 1) : 0)));
        }
        if (result.trace == null) File.Delete(tracePath); // Only this match's generated spool, never user data.
        return result;
    }

    public static bool IsTruncated(string status) => status == "TurnLimit" || status == "ActionLimit" || status == "TimeLimit";
    private static int Reward(int winner, int player) => winner < 0 ? 0 : winner == player ? 1 : -1;
    private static double Percentile(List<double> sorted, double p) => sorted[Math.Max(0, (int)Math.Ceiling(sorted.Count * p) - 1)];
}
