using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

// This component exists only in the separate simulator scene/build.
public sealed class SimulationRunner : MonoBehaviour
{
    public SimulationCatalog catalog;
    private SimulationConfig config;
    private string runDirectory, progressPath;
    private int expected;
    private readonly List<SimulationMatchResult> completed = new List<SimulationMatchResult>();
    private Dictionary<string, CardDefinition> definitions;

    private IEnumerator Start()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
        string[] arguments = Environment.GetCommandLineArgs();
        if (Array.IndexOf(arguments, "--replay-trace") >= 0)
        {
            try { SimulationReplay.RunCommandLine(catalog, arguments); }
            catch (Exception exception) { Debug.LogException(exception); Application.Quit(1); }
            yield break;
        }
        if (!Initialize()) yield break;
        var jobs = Jobs().ToArray(); expected = jobs.Length;
        try
        {
            Progress("warming-up", null);
            SimulationMatchResult mixed = jobs.FirstOrDefault(job => job.bot0 != job.bot1);
            SimulationMatchResult[] warmupTemplates = mixed != null
                ? new[] { mixed }
                : jobs.GroupBy(job => job.bot0).Select(group => group.First()).ToArray();
            for (int i = 0; i < config.warmupMatches; i++)
            {
                SimulationMatchResult template = warmupTemplates[i % warmupTemplates.Length];
                var warmup = new SimulationMatchResult { matchId = "warmup_" + i, pairId = "warmup", seed = (uint)(config.firstSeed + i),
                    bot0 = template.bot0, bot1 = template.bot1, deck0 = template.deck0, deck1 = template.deck1 };
                SimulationEngine.Run(config, Deck(warmup.deck0), Deck(warmup.deck1), warmup,
                    Path.Combine(runDirectory, "traces", warmup.matchId + ".jsonl"), false, r => Progress("warming-up", r));
                if (warmup.status != "Finished" && !SimulationEngine.IsTruncated(warmup.status))
                    throw new InvalidOperationException("Warm-up failed: " + warmup.status + " " + warmup.error);
            }
        }
        catch (Exception exception) { Fail(exception); yield break; }
        StreamWriter csv = null, jsonl = null;
        try
        {
            csv = new StreamWriter(Path.Combine(runDirectory, "matches.csv"), false, new System.Text.UTF8Encoding(true)) { AutoFlush = true };
            jsonl = new StreamWriter(Path.Combine(runDirectory, "matches.jsonl"), false, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
            csv.WriteLine("match_id,pair_id,scenario,seed,leg,bot0,bot1,deck0,deck1,first_player,status,winner,turns,actions,decisions0,decisions1,lp0,lp1,energy_spent0,energy_spent1,energy_allocated0,energy_allocated1,unused_at_turn_end0,unused_at_turn_end1,mean_decision_ms0,mean_decision_ms1,median_decision_ms0,median_decision_ms1,p95_decision_ms0,p95_decision_ms1,max_decision_ms0,max_decision_ms1,preparation_ms0,preparation_ms1,apply_ms0,apply_ms1,slow_decisions,wall_seconds,action_digest,result_digest,trace,error,final_state_hash");
        }
        catch (Exception exception)
        {
            CloseResultWriters(csv, jsonl);
            Fail(exception);
            yield break;
        }
        Exception closeFailure = null;
        try
        {
            for (int i = 0; i < jobs.Length; i++)
            {
                try
                {
                    SimulationMatchResult result = jobs[i];
                    SimulationEngine.Run(config, Deck(result.deck0), Deck(result.deck1), result,
                        Path.Combine(runDirectory, "traces", result.matchId + ".jsonl"), i < config.detailedMatches,
                        r => Progress("running", r));
                    jsonl.WriteLine(SimulationJson.Encode(result));
                    csv.WriteLine(Csv(result)); completed.Add(result);
                    if (completed.Count % 10 == 0 || completed.Count == expected)
                        SimulationJson.Atomic(Path.Combine(runDirectory, "summary.json"), SimulationSummary.Create(completed, config, false));
                    Progress("running", null);
                }
                catch (Exception exception) { Fail(exception); yield break; }
                yield return null;
            }
        }
        finally { closeFailure = CloseResultWriters(csv, jsonl); }
        if (closeFailure != null) { Fail(closeFailure); yield break; }
        try
        {
            SimulationJson.Atomic(Path.Combine(runDirectory, "summary.json"), SimulationSummary.Create(completed, config));
            int errors = completed.Count(r => r.status != "Finished" && !SimulationEngine.IsTruncated(r.status));
            Progress(errors == 0 ? "completed" : "completed-with-errors", null);
            Application.Quit(errors == 0 ? 0 : 2);
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static Exception CloseResultWriters(StreamWriter csv, StreamWriter jsonl)
    {
        Exception failure = null;
        try { csv?.Dispose(); } catch (Exception exception) { failure = exception; }
        try { jsonl?.Dispose(); } catch (Exception exception) { if (failure == null) failure = exception; }
        return failure;
    }

    private bool Initialize()
    {
        try
        {
            string configPath = Argument("--simulation-config");
            progressPath = Argument("--simulation-progress");
            config = JsonUtility.FromJson<SimulationConfig>(File.ReadAllText(configPath));
            Validate();
            string root = SimulationJson.ResultsRoot(config.projectRoot);
            string project = Path.GetFullPath(config.projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(progressPath).StartsWith(project, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Progress file must also be inside the project on D.");
            runDirectory = Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(Path.Combine(runDirectory, "traces"));
            definitions = catalog.cards.ToDictionary(card => card.CardId, CardDataConverter.ToDefinition);
            for (int i = 0; i < catalog.presets.Length; i++) Deck(i);
            SimulationJson.Atomic(Path.Combine(runDirectory, "run.json"), SimulationJson.Object("schema", 2,
                "createdUtc", DateTime.UtcNow.ToString("o"), "config", config, "rules", GameConfig.CreateDefault(),
                "rulesVersion", MatchHeader.CurrentRulesVersion, "catalogVersion", MatchHeader.CurrentCardCatalogVersion,
                "randomVersion", SeededGameRandom.AlgorithmVersion, "sourceRevision", catalog.sourceRevision,
                "sourceFingerprint", catalog.sourceFingerprint, "catalogFingerprint", catalog.catalogFingerprint,
                "unityVersion", Application.unityVersion, "runtime", Environment.Version.ToString(),
                "machine", Environment.MachineName, "os", SystemInfo.operatingSystem, "cpu", SystemInfo.processorType,
                "deckDefinitions", catalog.presets, "cardDefinitions", definitions.Values.OrderBy(c => c.CardId).ToArray(),
                "traceSchema", SimulationReplayFormat.TraceSchema, "stateHashSchema", SimulationStateHash.Schema,
                "aiContract", SimulationAiContract.Describe(),
                "digestScope", "actionDigest/resultDigest keep their legacy scope. finalStateHash and trace stateHash cover the pure private state graph; replay verification is a separate command and report.",
                "observationSchema", "Shared bot view plus owned/public Normal context. Diagnostic run metadata/seed/deck lists are not policy input."));
            return true;
        }
        catch (Exception exception) { Fail(exception); return false; }
    }

    private void Validate()
    {
        if (config == null || config.schemaVersion != 1 || catalog == null) throw new InvalidOperationException("Unsupported/missing configuration or catalog.");
        SimulationJson.ResultsRoot(config.projectRoot);
        if (config.seedCount < 1 || config.seedCount > 10000 || config.firstSeed < 1 || (long)config.firstSeed + config.seedCount > uint.MaxValue ||
            config.maxTurns < 1 || config.maxActions < 1 || config.maxMatchSeconds <= 0 || config.decisionWarningMs <= 0 ||
            config.warmupMatches < 0 || config.warmupMatches > 20 || config.detailedMatches < 0 || config.matchups == null || config.matchups.Length == 0)
            throw new InvalidOperationException("Invalid batch limits/seeds/matchups.");
        foreach (SimulationMatchup matchup in config.matchups)
        {
            SimulationEngine.Bot(matchup.player0Bot); SimulationEngine.Bot(matchup.player1Bot);
            if (matchup.deck0 < 0 || matchup.deck1 < 0 || matchup.deck0 >= catalog.presets.Length || matchup.deck1 >= catalog.presets.Length)
                throw new InvalidOperationException("Deck index is outside the packaged preset catalog.");
        }
    }

    private List<CardDefinition> Deck(int index)
    {
        SavedDeck saved = catalog.presets[index];
        var cards = catalog.cards.ToDictionary(card => card.CardId);
        if (!LocalDeckStore.Validate(saved, cards, GameConfig.CreateDefault(), out string reason)) throw new InvalidOperationException(reason);
        return saved.cardIds.Select(id => definitions[id]).ToList();
    }

    private IEnumerable<SimulationMatchResult> Jobs()
    {
        for (int scenario = 0; scenario < config.matchups.Length; scenario++)
        for (int seed = 0; seed < config.seedCount; seed++)
        {
            SimulationMatchup matchup = config.matchups[scenario];
            string pair = "s" + scenario + "_seed" + ((uint)config.firstSeed + seed);
            for (int leg = 0; leg < (matchup.swapBots ? 2 : 1); leg++)
                yield return new SimulationMatchResult { matchId = pair + "_leg" + leg, pairId = pair, scenario = scenario,
                    seed = (uint)config.firstSeed + (uint)seed, leg = leg, deck0 = matchup.deck0, deck1 = matchup.deck1,
                    bot0 = leg == 0 ? matchup.player0Bot : matchup.player1Bot, bot1 = leg == 0 ? matchup.player1Bot : matchup.player0Bot };
        }
    }

    private void Progress(string state, SimulationMatchResult active)
    {
        var snapshot = SimulationJson.Object("state", state, "utc", DateTime.UtcNow.ToString("o"), "completed", completed.Count,
            "expected", expected, "runDirectory", runDirectory, "activeMatch", active?.matchId, "turn", active?.turns, "actions", active?.actions);
        SimulationJson.Atomic(progressPath, snapshot);
        SimulationJson.Atomic(Path.Combine(runDirectory, "progress.json"), snapshot);
    }

    private void Fail(Exception exception)
    {
        var error = SimulationJson.Object("state", "failed", "error", exception.ToString(), "runDirectory", runDirectory);
        // Only write after a validated project root; never use default C-drive paths.
        try
        {
            if (config != null && progressPath != null)
            {
                string root = Path.GetFullPath(config.projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                SimulationJson.ResultsRoot(config.projectRoot);
                if (Path.GetFullPath(progressPath).StartsWith(root, StringComparison.OrdinalIgnoreCase)) SimulationJson.Atomic(progressPath, error);
            }
            if (runDirectory != null) SimulationJson.Atomic(Path.Combine(runDirectory, "failure.json"), error);
        }
        finally { Debug.LogError(exception); Application.Quit(1); }
    }

    private static string Argument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, name);
        if (index < 0 || index + 1 >= arguments.Length) throw new ArgumentException("Missing " + name);
        return Path.GetFullPath(arguments[index + 1]);
    }

    private static string Csv(SimulationMatchResult r)
    {
        object[] fields = { r.matchId,r.pairId,r.scenario,r.seed,r.leg,r.bot0,r.bot1,r.deck0,r.deck1,r.firstPlayer,r.status,r.winner,r.turns,r.actions,
            r.decisions[0],r.decisions[1],r.finalLp[0],r.finalLp[1],r.energySpent[0],r.energySpent[1],r.energyAllocated[0],r.energyAllocated[1],
            r.unusedAtTurnEnd[0],r.unusedAtTurnEnd[1],r.meanDecisionMs[0],r.meanDecisionMs[1],r.medianDecisionMs[0],r.medianDecisionMs[1],
            r.p95DecisionMs[0],r.p95DecisionMs[1],r.maxDecisionMs[0],r.maxDecisionMs[1],r.preparationMs[0],r.preparationMs[1],r.applyMs[0],r.applyMs[1],
            r.slowDecisions,r.durationSeconds,r.actionDigest,r.resultDigest,r.trace,r.error,r.finalStateHash };
        return string.Join(",", fields.Select(value => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\""));
    }
}
