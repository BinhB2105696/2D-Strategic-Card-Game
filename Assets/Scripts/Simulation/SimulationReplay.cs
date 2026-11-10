using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class SimulationReplayReport
{
    public int schema = 1, sequence, verifiedActions, verifiedDecisions;
    public bool verified, completeMatch, truncated;
    public string status, component, expected, actual, error, trace, dataset, partialDataset;
}

public static class SimulationReplay
{
    [Serializable] private sealed class Metadata
    {
        public int schema, rulesVersion, catalogVersion, randomVersion;
        public string sourceFingerprint, catalogFingerprint, unityVersion, runtime;
        public SimulationConfig config;
    }
    [Serializable] private sealed class RecordedAction
    {
        public string type, position;
        public int player, zone, amount;
        public long source;
        public long[] targets, tributes;
        public string Key() => SimulationJson.Encode(SimulationJson.Object("type", type, "position", position,
            "player", player, "zone", zone, "amount", amount, "source", source,
            "targets", targets ?? new long[0], "tributes", tributes ?? new long[0]));
    }
    [Serializable] private sealed class RecordedResult
    {
        public string status, actionDigest, resultDigest, finalStateHash;
        public int winner, turns, actions;
        public int[] finalLp;
    }
    [Serializable] private sealed class RecordedEvent
    {
        public string Type, Phase;
        public int Amount, PlayerIndex;
        public long CardInstanceId, TargetInstanceId;
    }
    [Serializable] private sealed class Line
    {
        public string record, matchId, stateHash, rulesHash, definitionsHash, aiDescriptionHash;
        public string phase, eventsHash, observationJson, policyInputJson, policyHash;
        public int schema, stateHashSchema, aiSchema, sequence, turn, selectedActionIndex, reward0, reward1;
        public uint seed;
        public bool detailed, automatic, terminated, truncated;
        public string[] deck0, deck1;
        public RecordedAction action;
        public RecordedAction[] legalActions;
        public RecordedEvent[] events;
        public RecordedResult result;
    }
    private sealed class Mismatch : Exception
    {
        public readonly string Component, Expected, Actual;
        public Mismatch(string component, object expected, object actual) : base("Replay mismatch: " + component)
        { Component = component; Expected = Convert.ToString(expected); Actual = Convert.ToString(actual); }
    }
    private static void Equal(string component, object expected, object actual)
    { if (!Equals(expected, actual)) throw new Mismatch(component, expected, actual); }

    public static string InsideProject(string projectRoot, string path)
    {
        SimulationJson.ResultsRoot(projectRoot);
        string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Replay paths must remain inside this project on D.");
        return full;
    }

    public static SimulationReplayReport Verify(string projectRoot, string runDirectory, string tracePath,
        SimulationCatalog catalog, string datasetPath = null)
    {
        var report = new SimulationReplayReport { status = "invalid", trace = tracePath };
        StreamWriter dataset = null;
        string pendingPath = null;
        try
        {
            runDirectory = InsideProject(projectRoot, runDirectory);
            tracePath = InsideProject(projectRoot, tracePath);
            string traceRoot = Path.GetFullPath(Path.Combine(runDirectory, "traces")) + Path.DirectorySeparatorChar;
            if (!tracePath.StartsWith(traceRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Trace must belong to the selected run/traces directory.");
            Metadata meta = JsonUtility.FromJson<Metadata>(File.ReadAllText(Path.Combine(runDirectory, "run.json")));
            if (meta == null || meta.config == null || catalog == null) throw new InvalidOperationException("Missing run metadata/config/catalog.");
            Equal("run schema (legacy traces cannot be fully verified)", 2, meta.schema);
            Equal("runtime source fingerprint", catalog.sourceFingerprint, meta.sourceFingerprint);
            Equal("card catalog fingerprint", catalog.catalogFingerprint, meta.catalogFingerprint);
            Equal("rules version", MatchHeader.CurrentRulesVersion, meta.rulesVersion);
            Equal("catalog version", MatchHeader.CurrentCardCatalogVersion, meta.catalogVersion);
            Equal("RNG version", SeededGameRandom.AlgorithmVersion, meta.randomVersion);
            Equal("Unity version", Application.unityVersion, meta.unityVersion);
            Equal("runtime version", Environment.Version.ToString(), meta.runtime);
            Equal("projectRoot", Path.GetFullPath(projectRoot), Path.GetFullPath(meta.config.projectRoot));
            var definitions = catalog.cards.ToDictionary(card => card.CardId, CardDataConverter.ToDefinition);
            using (var reader = new StreamReader(tracePath))
            {
                string first = reader.ReadLine();
                if (first == null) throw new InvalidOperationException("Empty trace.");
                Line header = JsonUtility.FromJson<Line>(first);
                Equal("first record", "start", header.record);
                Equal("trace schema", SimulationReplayFormat.TraceSchema, header.schema);
                Equal("state hash schema", SimulationStateHash.Schema, header.stateHashSchema);
                Equal("AI schema", SimulationAiContract.Schema, header.aiSchema);
                Equal("AI description", SimulationJson.Hash(SimulationJson.Encode(SimulationAiContract.Describe())), header.aiDescriptionHash);
                Equal("rules hash", SimulationReplayFormat.RulesHash(), header.rulesHash);
                if (header.deck0 == null || header.deck1 == null || header.seed == 0)
                    throw new InvalidOperationException("Missing replay decks/seed.");
                var deck0 = header.deck0.Select(id => definitions[id]).ToList();
                var deck1 = header.deck1.Select(id => definitions[id]).ToList();
                Equal("card definitions", SimulationReplayFormat.DefinitionsHash(deck0.Concat(deck1)), header.definitionsHash);
                GameSession session = GameSession.Create(GameConfig.CreateDefault(), header.seed, deck0, deck1);
                Equal("initial state", header.stateHash, SimulationStateHash.Capture(session.State));
                if (datasetPath != null)
                {
                    if (!header.detailed) throw new InvalidOperationException("AI export requires a detailed trace; rerun with detailedMatches enabled.");
                    datasetPath = InsideProject(projectRoot, datasetPath);
                    pendingPath = datasetPath + ".tmp";
                    if (File.Exists(datasetPath) || File.Exists(pendingPath)) throw new IOException("AI output already exists; choose a new path.");
                    Directory.CreateDirectory(Path.GetDirectoryName(datasetPath));
                    dataset = new StreamWriter(new FileStream(pendingPath, FileMode.CreateNew), new UTF8Encoding(false));
                    dataset.WriteLine(SimulationJson.Encode(SimulationJson.Object("record", "dataset_start", "schema", SimulationAiContract.Schema,
                        "contract", SimulationAiContract.Describe(), "matchId", header.matchId,
                        "sourceTrace", tracePath, "sourceFingerprint", meta.sourceFingerprint, "catalogFingerprint", meta.catalogFingerprint)));
                }
                Line pending = null;
                GameAction selected = null;
                var transitions = new Dictionary<string, object>[2];
                var actions = new StringBuilder();
                bool ended = false;
                string text;
                while ((text = reader.ReadLine()) != null)
                {
                    if (ended) throw new InvalidOperationException("Unexpected data after end record.");
                    Line line = JsonUtility.FromJson<Line>(text);
                    if (line == null) throw new InvalidOperationException("Invalid trace record.");
                    GameState state = session.State;
                    if (line.record == "attempt")
                    {
                        report.sequence = line.sequence;
                        if (pending != null) throw new InvalidOperationException("Attempt without its accepted record.");
                        Equal("sequence", report.verifiedActions + 1, line.sequence);
                        Equal("turn", state.TurnNumber, line.turn);
                        Equal("phase", state.CurrentPhase.ToString(), line.phase);
                        Equal("before state", line.stateHash, SimulationStateHash.Capture(state));
                        if (line.action == null) throw new InvalidOperationException("Missing action.");
                        GameAction automatic = SimulationEngine.AutomaticAction(state);
                        Equal("automatic flag", automatic != null, line.automatic);
                        if (line.automatic) Equal("automatic selected index", -1, line.selectedActionIndex);
                        IReadOnlyList<GameAction> legal = automatic == null
                            ? GameRules.GetLegalActions(state, state.InteractionPlayerIndex) : new[] { automatic };
                        string key = line.action.Key();
                        selected = legal.FirstOrDefault(action => SimulationJson.Encode(SimulationJson.Action(action)) == key);
                        if (selected == null) throw new Mismatch("legal action", "a current legal candidate", key);
                        if (!line.automatic)
                        {
                            Equal("selected index", SimulationAiContract.SelectedIndex(legal, selected), line.selectedActionIndex);
                            if (header.detailed)
                            {
                                Equal("observation", SimulationJson.Encode(SimulationObservation.Capture(state, selected.PlayerIndex)), line.observationJson);
                                Equal("legal candidate order", SimulationJson.Encode(legal.Select(SimulationJson.Action).ToArray()),
                                    SimulationJson.Encode((line.legalActions ?? new RecordedAction[0]).Select(a => (object)JsonAction(a)).ToArray()));
                                string input = SimulationJson.Encode(SimulationAiContract.Capture(state, selected.PlayerIndex, legal));
                                Equal("policy input", input, line.policyInputJson);
                                Equal("policy input hash", SimulationJson.Hash(input), line.policyHash);
                            }
                            report.verifiedDecisions++;
                            if (dataset != null && transitions[selected.PlayerIndex] != null)
                            {
                                var previous = transitions[selected.PlayerIndex];
                                previous["nextPolicyInputJson"] = line.policyInputJson;
                                dataset.WriteLine(SimulationJson.Encode(previous));
                                transitions[selected.PlayerIndex] = null;
                            }
                        }
                        pending = line;
                    }
                    else if (line.record == "accepted")
                    {
                        if (pending == null) throw new InvalidOperationException("Accepted record without an attempt.");
                        Equal("accepted sequence", pending.sequence, line.sequence);
                        ActionResult applied = session.TryApply(selected);
                        if (!applied.Succeeded) throw new InvalidOperationException("Core rejected replay action: " + applied.ErrorMessage);
                        Equal("after state", line.stateHash, SimulationStateHash.Capture(state));
                        if (line.events == null) throw new InvalidOperationException("Missing ordered event payload.");
                        Equal("recorded event payload", line.eventsHash, SimulationJson.Hash(SimulationJson.Encode(line.events.Select(JsonEvent).ToArray())));
                        Equal("ordered events", line.eventsHash, SimulationJson.Hash(SimulationJson.Encode(applied.Events)));
                        Equal("terminated", state.IsFinished, line.terminated);
                        Equal("reward0", SimulationAiContract.Reward(state, 0), line.reward0);
                        Equal("reward1", SimulationAiContract.Reward(state, 1), line.reward1);
                        actions.Append(SimulationJson.Encode(SimulationJson.Action(selected))).Append('\n');
                        report.verifiedActions++;
                        if (dataset != null && !pending.automatic)
                        {
                            int player = selected.PlayerIndex;
                            transitions[player] = SimulationJson.Object("record", "transition", "schema", SimulationAiContract.Schema,
                                "sequence", line.sequence, "player", player, "policyInputJson", pending.policyInputJson,
                                "selectedActionIndex", pending.selectedActionIndex, "reward", 0,
                                "terminated", false, "truncated", false);
                        }
                        if (dataset != null && state.IsFinished)
                            for (int actor = 0; actor < 2; actor++)
                                if (transitions[actor] != null) transitions[actor]["reward"] = SimulationAiContract.Reward(state, actor);
                        pending = null;
                    }
                    else if (line.record == "end")
                    {
                        if (pending != null || line.result == null) throw new InvalidOperationException("Incomplete action/end record.");
                        RecordedResult result = line.result;
                        bool truncated = SimulationEngine.IsTruncated(result.status);
                        if (result.status != "Finished" && !truncated) throw new InvalidOperationException("Failed source episode: " + result.status);
                        Equal("end terminated", state.IsFinished, line.terminated);
                        Equal("end status", state.IsFinished ? "Finished" : result.status, result.status);
                        if (!state.IsFinished && !truncated) throw new InvalidOperationException("Unfinished source episode.");
                        Equal("end truncated", truncated, line.truncated);
                        Equal("end actions", report.verifiedActions, result.actions);
                        Equal("end turns", state.TurnNumber, result.turns);
                        Equal("end winner", state.IsFinished ? state.WinnerPlayerIndex : -2, result.winner);
                        Equal("end LP", SimulationJson.Encode(state.Players.Select(p => p.Health).ToArray()), SimulationJson.Encode(result.finalLp));
                        Equal("end state", line.stateHash, SimulationStateHash.Capture(state));
                        Equal("result state", line.stateHash, result.finalStateHash);
                        Equal("action digest", SimulationJson.Hash(actions.ToString()), result.actionDigest);
                        Equal("result digest", SimulationJson.Hash(SimulationJson.Encode(SimulationJson.Object("status", result.status,
                            "winner", result.winner, "turns", result.turns, "lp", result.finalLp, "actions", result.actions,
                            "actionDigest", result.actionDigest))), result.resultDigest);
                        Equal("end reward0", SimulationAiContract.Reward(state, 0), line.reward0);
                        Equal("end reward1", SimulationAiContract.Reward(state, 1), line.reward1);
                        report.completeMatch = state.IsFinished; report.truncated = truncated;
                        if (dataset != null)
                        {
                            for (int actor = 0; actor < 2; actor++)
                            {
                                var transition = transitions[actor];
                                if (transition == null) continue;
                                IReadOnlyList<GameAction> next = !state.IsFinished && state.InteractionPlayerIndex == actor && SimulationEngine.AutomaticAction(state) == null
                                    ? GameRules.GetLegalActions(state, actor) : new GameAction[0];
                                transition["nextPolicyInputJson"] = SimulationJson.Encode(SimulationAiContract.Capture(state, actor, next));
                                transition["terminated"] = state.IsFinished;
                                transition["truncated"] = truncated;
                                dataset.WriteLine(SimulationJson.Encode(transition));
                            }
                            dataset.WriteLine(SimulationJson.Encode(SimulationJson.Object("record", "episode_end", "status", result.status,
                                "terminated", state.IsFinished, "truncated", truncated, "winner", result.winner,
                                "reward0", line.reward0, "reward1", line.reward1)));
                        }
                        ended = true;
                    }
                    else throw new InvalidOperationException("Unknown record: " + line.record);
                }
                if (!ended) throw new InvalidOperationException("Incomplete trace: missing end record.");
            }
            dataset?.Dispose(); dataset = null;
            if (datasetPath != null) { File.Move(pendingPath, datasetPath); report.dataset = datasetPath; }
            report.verified = true; report.status = report.completeMatch ? "verified-match" : "verified-prefix";
        }
        catch (Mismatch mismatch)
        { report.component = mismatch.Component; report.expected = mismatch.Expected; report.actual = mismatch.Actual; report.error = mismatch.Message; }
        catch (Exception exception) { report.component = "format/compatibility/storage"; report.error = exception.ToString(); }
        finally
        {
            try { dataset?.Dispose(); } catch (Exception exception) { report.verified = false; report.status = "invalid"; report.error = exception.ToString(); }
            if (!report.verified && pendingPath != null && File.Exists(pendingPath)) report.partialDataset = pendingPath;
        }
        return report;
    }

    private static object JsonAction(RecordedAction action) => SimulationJson.Object("type", action.type,
        "position", action.position, "player", action.player, "zone", action.zone, "amount", action.amount,
        "source", action.source, "targets", action.targets ?? new long[0], "tributes", action.tributes ?? new long[0]);

    private static object JsonEvent(RecordedEvent entry) => SimulationJson.Object("Type", entry.Type, "Phase", entry.Phase,
        "Amount", entry.Amount, "PlayerIndex", entry.PlayerIndex, "CardInstanceId", entry.CardInstanceId, "TargetInstanceId", entry.TargetInstanceId);

    public static void RunCommandLine(SimulationCatalog catalog, string[] arguments)
    {
        Func<string, string> value = name => { int i = Array.IndexOf(arguments, name); return i >= 0 && i + 1 < arguments.Length ? arguments[i + 1] : null; };
        string project = value("--simulation-project"), output = value("--replay-report");
        output = InsideProject(project, output);
        if (File.Exists(output)) throw new IOException("Replay report already exists.");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = Verify(project, value("--replay-run"), value("--replay-trace"), catalog, value("--ai-export"));
        SimulationJson.Atomic(output, report);
        Application.Quit(report.verified ? 0 : 1);
    }
}
