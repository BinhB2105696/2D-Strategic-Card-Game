using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

// Bounded single-observer information-set MCTS. All transitions use the actual rules.
// The catalog is public game content, never a catalog inferred from the two true decks.
public sealed class HardBot : IBot, IStateObservingBot
{
    public const string AlgorithmVersion = "ismcts-H5.4-primary-plan-sequential-fallback-v1";
    private readonly CardDefinition[] catalog;
    private readonly CardDefinition[] knownOwnStartingDeck;
    private readonly NormalBot fallback = new NormalBot();
    private readonly NormalBot[] rolloutBots = { new NormalBot(), new NormalBot() };
    private readonly HardTacticalPolicy tactics;
    private readonly HardTacticalPolicy[] searchTactics;
    private readonly int iterations, treeDepth, rolloutSteps;
    private readonly uint policySeed;
    private readonly bool pairedRootSampling;
    private readonly int turnHorizon;
    private uint decision;
    private GameState source;
    private int observer;
    public string DisplayName => "Hard Bot";
    public int LastIterations { get; private set; }
    public int LastNodes { get; private set; }
    public int LastRuleApplications { get; private set; }
    public double LastSearchMilliseconds { get; private set; }
    public string LastFallbackReason { get; private set; }
    public bool LastChangedBaseline { get; private set; }
    public int LastMaxTreeDepth { get; private set; }
    public int LastRolloutActions { get; private set; }
    public static event Action<IReadOnlyDictionary<string, object>> DecisionDiagnostics;
    public IReadOnlyDictionary<string, object> Describe() => new Dictionary<string, object> {
        { "algorithm", AlgorithmVersion }, { "iterations", iterations }, { "treeDepth", treeDepth },
        { "rolloutSteps", rolloutSteps }, { "policySeed", policySeed }, { "rootHeuristicCandidates", 12 },
        { "hiddenPrior", "uniform-public-catalog-with-copy-limits" },
        { "ownDeckPrior", knownOwnStartingDeck == null ? "public-catalog" : "initial-deck-minus-observed-owned-cards" },
        { "pairedRootSampling", pairedRootSampling }, { "baselineImprovementMargin", 0.005 },
        { "turnHorizon", turnHorizon }, { "responseRoots", "Normal fallback" },
        { "tactics", "prospective-passives-hand-board-upgrades-draw-primary-plan-sequential-fallback-v10" } };

    private sealed class Node
    {
        public int Visits;
        public double Value;
        public readonly Dictionary<string, Edge> Edges = new Dictionary<string, Edge>(StringComparer.Ordinal);
    }
    private sealed class Edge
    {
        public int Visits, Availability;
        public double Value;
        public readonly Dictionary<string, Node> Outcomes = new Dictionary<string, Node>(StringComparer.Ordinal);
    }
    private sealed class Choice
    {
        public string Key;
        public GameAction Action;
        public Edge Edge;
        public readonly List<GameAction> Members = new List<GameAction>();
    }

    public HardBot(IReadOnlyList<CardDefinition> publicCatalog, int iterations = 96,
        int treeDepth = 6, int rolloutSteps = 64, uint policySeed = 0x48A2D31u,
        IReadOnlyList<CardDefinition> knownOwnStartingDeck = null, bool pairedRootSampling = true, int turnHorizon = 2)
    {
        if (publicCatalog == null || publicCatalog.Count == 0) throw new ArgumentException("Hard requires the full public catalog.");
        if (iterations < 1 || treeDepth < 1 || rolloutSteps < 1) throw new ArgumentOutOfRangeException();
        if (turnHorizon < 1 || turnHorizon > 2) throw new ArgumentOutOfRangeException(nameof(turnHorizon));
        catalog = publicCatalog.OrderBy(card => card.CardId, StringComparer.Ordinal).ToArray();
        if (catalog.Select(card => card.CardId).Distinct(StringComparer.Ordinal).Count() != catalog.Length)
            throw new ArgumentException("Duplicate card IDs in public catalog.");
        this.iterations = iterations; this.treeDepth = treeDepth; this.rolloutSteps = rolloutSteps; this.policySeed = policySeed;
        this.knownOwnStartingDeck = knownOwnStartingDeck?.ToArray();
        this.pairedRootSampling = pairedRootSampling;
        this.turnHorizon = turnHorizon;
        tactics = new HardTacticalPolicy(catalog);
        searchTactics = new[] { new HardTacticalPolicy(catalog), new HardTacticalPolicy(catalog) };
    }

    public void Observe(GameState state, int player)
    {
        source = null;
        if (state == null || player != state.InteractionPlayerIndex) throw new ArgumentException("Hard observation must match the acting player.");
        observer = player;
        fallback.Observe(state, player);
        tactics.Observe(state, player);
        // The live reference is used only by the sanitization boundary, then discarded after this decision.
        source = state;
    }

    public GameAction ChooseAction(BotGameStateView view, IReadOnlyList<GameAction> legalActions)
    {
        GameAction normalChoice = fallback.ChooseAction(view, legalActions);
        GameAction chosen = SearchAction(view, legalActions, normalChoice);
        LastChangedBaseline = ActionKey(view, chosen) != ActionKey(view, normalChoice);
        tactics.Commit(chosen);
        if (DecisionDiagnostics != null) DecisionDiagnostics(new Dictionary<string, object> {
            { "iterations", LastIterations }, { "maxTreeDepth", LastMaxTreeDepth }, { "rolloutActions", LastRolloutActions },
            { "nodes", LastNodes }, { "changedBaseline", LastChangedBaseline }, { "fallback", LastFallbackReason },
            { "cancelledCommitments", tactics.LastCancelledCommitments },
            { "plannedAttackers", tactics.LastPlannedAttackers }, { "attackPlanScore", tactics.LastAttackPlanScore },
            { "attackPlanFallback", tactics.LastAttackPlanFallback } });
        return chosen;
    }

    private GameAction SearchAction(BotGameStateView view, IReadOnlyList<GameAction> legalActions, GameAction baseline)
    {
        if (view == null || legalActions == null || legalActions.Count == 0) throw new ArgumentException("Hard requires legal choices.");
        LastIterations = LastNodes = LastRuleApplications = 0; LastFallbackReason = null;
        LastChangedBaseline = false;
        LastMaxTreeDepth = LastRolloutActions = 0;
        var watch = Stopwatch.StartNew();
        try
        {
            if (source == null || view.ViewingPlayerIndex != observer) throw new InvalidOperationException("Observe must precede every Hard decision.");
            uint seed = unchecked(policySeed + 0x9E3779B9u * ++decision);
            if (legalActions.Count == 1) return baseline;
            if (view.HasOpenResponseWindow || view.HasPendingTriggeredEffect)
            { LastFallbackReason = "response-or-trigger-root"; return baseline; }
            legalActions = tactics.Filter(view, legalActions, fallback);
            baseline = tactics.Choose(view, legalActions, fallback);
            var random = new SeededGameRandom(seed);
            var root = new Node(); LastNodes = 1;
            var original = Group(view, legalActions, root);
            string baselineKey = ActionKey(view, baseline);
            // Bound root branching without changing legal moves: best heuristic options plus action-type diversity.
            var ranked = original.OrderByDescending(choice => tactics.Score(view, choice.Action, fallback)).ToList();
            var candidates = new HashSet<string>(ranked.Take(12).Select(choice => choice.Key), StringComparer.Ordinal) { baselineKey };
            foreach (var group in ranked.GroupBy(choice => choice.Action.Type)) candidates.Add(group.First().Key);
            if (pairedRootSampling)
            {
                // Compare each candidate against the baseline in the same sampled worlds. This reduces
                // noise from hidden hands/shuffles without giving the policy any actual hidden information.
                var paired = original.Where(choice => candidates.Contains(choice.Key))
                    .OrderBy(choice => choice.Key == baselineKey ? 0 : 1).Take(iterations).ToArray();
                var evidence = paired.ToDictionary(choice => choice.Key, choice => new List<double>(), StringComparer.Ordinal);
                int rounds = Math.Max(1, iterations / paired.Length);
                for (int round = 0; round < rounds; round++)
                {
                    uint worldSeed = random.NextUInt();
                    double baselineValue = 0;
                    foreach (Choice choice in paired)
                    {
                        GameState world;
                        try { world = source.CreateInformationSetFork(observer, catalog, worldSeed, knownOwnStartingDeck); }
                        catch (InvalidOperationException error)
                        { LastFallbackReason = "unsupported-information-set: " + error.Message; return baseline; }
                        double value = Visit(world, root, observer, new SeededGameRandom(worldSeed), candidates, baselineKey, choice.Key);
                        LastIterations++;
                        if (choice.Key == baselineKey) baselineValue = value;
                        evidence[choice.Key].Add(value - baselineValue);
                    }
                }
                Choice improved = paired.Where(choice => choice.Key != baselineKey)
                    .OrderByDescending(choice => ImprovementBound(evidence[choice.Key])).FirstOrDefault();
                if (improved != null && ImprovementBound(evidence[improved.Key]) > 0.005)
                { LastChangedBaseline = true; return improved.Action; }
                return baseline;
            }
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                GameState world;
                try { world = source.CreateInformationSetFork(observer, catalog, random.NextUInt(), knownOwnStartingDeck); }
                catch (InvalidOperationException error)
                { LastFallbackReason = "unsupported-information-set: " + error.Message; return baseline; }
                Visit(world, root, observer, random, candidates, baselineKey);
                LastIterations++;
            }
            Choice best = original.Where(choice => choice.Edge.Visits > 0)
                .OrderByDescending(choice => choice.Edge.Visits)
                .ThenByDescending(choice => choice.Edge.Value / choice.Edge.Visits)
                .ThenBy(choice => choice.Key == baselineKey ? 0 : 1).FirstOrDefault();
            LastChangedBaseline = best != null && best.Key != baselineKey;
            return best == null ? baseline : best.Action; // Always an object from the original supplied legal list.
        }
        finally { source = null; LastSearchMilliseconds = watch.Elapsed.TotalMilliseconds; }
    }

    private static double ImprovementBound(List<double> differences)
    {
        double mean = differences.Average();
        if (differences.Count < 2) return mean - 0.03;
        double variance = differences.Sum(value => (value - mean) * (value - mean)) / (differences.Count - 1);
        // A conservative selection heuristic, not a statistical confidence interval or strength guarantee.
        return mean - 0.5 * Math.Sqrt(variance / differences.Count);
    }

    private double Visit(GameState world, Node root, int rootPlayer, SeededGameRandom random,
        HashSet<string> rootCandidates, string baselineKey, string forcedRootKey = null)
    {
        var nodes = new List<Node> { root };
        var edges = new List<Edge>();
        int horizonTurn = world.TurnNumber + turnHorizon;
        searchTactics[rootPlayer].BeginSimulationFrom(tactics);
        Node node = root;
        for (int depth = 0; depth < treeDepth && !world.IsFinished; depth++)
        {
            AdvanceAutomatic(world);
            if (world.IsFinished || world.TurnNumber >= horizonTurn) break;
            int actor = world.InteractionPlayerIndex;
            var legal = GameRules.GetLegalActions(world, actor);
            if (legal.Count == 0) throw new InvalidOperationException("Search reached a state with no legal action.");
            var view = BotGameStateView.Create(world, actor);
            rolloutBots[actor].Observe(world, actor);
            if (actor == rootPlayer)
            {
                searchTactics[actor].Observe(world, actor);
                if (depth > 0) legal = searchTactics[actor].Filter(view, legal, rolloutBots[actor]);
            }
            // Keys are expressed from the root observer's information, even when the opponent acts.
            var choices = Group(BotGameStateView.Create(world, rootPlayer), legal, node, view);
            if (depth == 0) choices.RemoveAll(choice => !rootCandidates.Contains(choice.Key));
            if (choices.Count == 0) throw new InvalidOperationException("Sampled root lost every legal public action.");
            foreach (Choice choice in choices) choice.Edge.Availability++;
            Choice selected;
            var unvisited = choices.Where(choice => choice.Edge.Visits == 0).ToList();
            if (depth == 0 && forcedRootKey != null)
                selected = choices.FirstOrDefault(choice => choice.Key == forcedRootKey)
                    ?? throw new InvalidOperationException("Sampled world lost the paired public root action.");
            else if (unvisited.Count > 0)
            {
                rolloutBots[actor].Observe(world, actor);
                GameAction guided = actor == rootPlayer ? searchTactics[actor].Choose(view, legal, rolloutBots[actor])
                    : rolloutBots[actor].ChooseAction(view, legal);
                string guidedKey = depth == 0 ? baselineKey : ActionKey(BotGameStateView.Create(world, rootPlayer), guided, view);
                selected = unvisited.FirstOrDefault(choice => choice.Key == guidedKey)
                    ?? unvisited.OrderByDescending(choice => actor == rootPlayer
                        ? searchTactics[actor].Score(view, choice.Action, rolloutBots[actor])
                        : rolloutBots[actor].ScoreForSearch(view, choice.Action)).First();
            }
            else
                selected = choices.OrderByDescending(choice => (actor == rootPlayer ? 1 : -1) * choice.Edge.Value / choice.Edge.Visits
                    + 0.2 * Math.Sqrt(Math.Log(Math.Max(2, choice.Edge.Availability)) / choice.Edge.Visits)).First();
            GameAction simulatedAction = selected.Action;
            if (actor != rootPlayer && selected.Members.Count > 1)
            {
                // A concealed action group must preserve the acting opponent's own card choice.
                // Root statistics merge indistinguishable Sets/discards, without replacing their policy by the first hand slot.
                rolloutBots[actor].Observe(world, actor);
                simulatedAction = rolloutBots[actor].ChooseAction(view, selected.Members);
            }
            if (actor == rootPlayer) searchTactics[actor].Commit(simulatedAction);
            Apply(world, simulatedAction); edges.Add(selected.Edge);
            LastMaxTreeDepth = Math.Max(LastMaxTreeDepth, depth + 1);
            AdvanceAutomatic(world);
            string outcome = InformationOutcome(world, rootPlayer);
            bool expanded = !selected.Edge.Outcomes.TryGetValue(outcome, out Node next);
            if (expanded) { next = new Node(); selected.Edge.Outcomes.Add(outcome, next); LastNodes++; }
            nodes.Add(next); node = next;
            if (expanded) break;
        }
        for (int step = 0; step < rolloutSteps && !world.IsFinished; step++)
        {
            AdvanceAutomatic(world);
            if (world.IsFinished || world.TurnNumber >= horizonTurn) break;
            int actor = world.InteractionPlayerIndex;
            var legal = GameRules.GetLegalActions(world, actor);
            if (legal.Count == 0) throw new InvalidOperationException("Rollout reached a state with no legal action.");
            rolloutBots[actor].Observe(world, actor);
            var rolloutView = BotGameStateView.Create(world, actor);
            GameAction rolloutAction;
            if (actor == rootPlayer)
            {
                searchTactics[actor].Observe(world, actor);
                rolloutAction = searchTactics[actor].Choose(rolloutView, legal, rolloutBots[actor]);
                searchTactics[actor].Commit(rolloutAction);
            }
            else rolloutAction = rolloutBots[actor].ChooseAction(rolloutView, legal);
            Apply(world, rolloutAction);
            LastRolloutActions++;
        }
        AdvanceAutomatic(world);
        double reward = Evaluate(world, rootPlayer);
        foreach (Node visited in nodes) { visited.Visits++; visited.Value += reward; }
        foreach (Edge visited in edges) { visited.Visits++; visited.Value += reward; }
        return reward;
    }

    private void Apply(GameState world, GameAction action)
    {
        ActionResult applied = GameRules.ApplyAction(world, action); LastRuleApplications++;
        if (!applied.Succeeded) throw new InvalidOperationException("Search action rejected by game rules: " + applied.ErrorMessage);
    }
    private void AdvanceAutomatic(GameState world)
    {
        for (int guard = 0; guard < 8 && !world.IsFinished && !world.HasOpenResponseWindow && !world.HasPendingTriggeredEffect; guard++)
        {
            int actor = world.InteractionPlayerIndex;
            if (world.CurrentPhase == TurnPhase.Standby) Apply(world, GameAction.AdvancePhase(actor));
            else if (world.CurrentPhase == TurnPhase.Recovery)
                Apply(world, world.CurrentRecoveryStep == RecoveryStep.Draw ? GameAction.ResolveDrawStep(actor) : GameAction.ResolveEnergyRefreshStep(actor));
            else break;
        }
    }
    private static List<Choice> Group(BotGameStateView publicView, IReadOnlyList<GameAction> legal, Node node, BotGameStateView actorView = null)
    {
        var choices = new List<Choice>(); var groups = new Dictionary<string, Choice>(StringComparer.Ordinal);
        foreach (GameAction action in legal)
        {
            string key = ActionKey(publicView, action, actorView);
            if (groups.TryGetValue(key, out Choice grouped)) { grouped.Members.Add(action); continue; }
            if (!node.Edges.TryGetValue(key, out Edge edge)) { edge = new Edge(); node.Edges.Add(key, edge); }
            var choice = new Choice { Key = key, Action = action, Edge = edge };
            choice.Members.Add(action); groups.Add(key, choice); choices.Add(choice);
        }
        return choices;
    }
    private static string CardKey(BotGameStateView view, long id)
    {
        if (id == 0) return "none";
        BotCardView card = view.FindCard(id);
        if (card == null) return "concealed";
        string slot = "";
        var owner = view.Players[card.OwnerPlayerIndex];
        var zones = card.Zone == CardZone.MonsterZone ? owner.MonsterZones : owner.SpellTrapZones;
        if (card.Zone == CardZone.MonsterZone || card.Zone == CardZone.SpellTrapZone)
            for (int i = 0; i < zones.Count; i++) if (zones[i]?.InstanceId == id) slot = i.ToString();
        return card.OwnerPlayerIndex + ":" + card.Zone + ":" + slot + ":" + (card.IsKnown ? card.CardId : "hidden");
    }
    private static string ActionKey(BotGameStateView view, GameAction action, BotGameStateView actorView = null)
    {
        // Opponent hand actions reveal identity when activated/summoned, except setting a concealed trap.
        // Revealing actions may use the acting player's card identity; a Set action must not do so.
        var key = new StringBuilder().Append(action.PlayerIndex).Append('|').Append(action.Type).Append('|')
            .Append(action.ZoneIndex).Append('|').Append(action.Amount).Append('|').Append(action.BattlePosition).Append('|');
        key.Append(action.Type == GameActionType.SetTrap && action.PlayerIndex != view.ViewingPlayerIndex
            ? "concealed-set" : CardKey(view.FindCard(action.SourceInstanceId) != null || actorView == null ? view : actorView, action.SourceInstanceId));
        foreach (long id in action.TargetInstanceIds) key.Append('|').Append(CardKey(view, id));
        key.Append("|tributes");
        foreach (string tribute in action.TributeInstanceIds.Select(id => CardKey(view, id)).OrderBy(value => value, StringComparer.Ordinal)) key.Append('|').Append(tribute);
        return key.ToString();
    }
    private static string InformationOutcome(GameState world, int rootPlayer)
    {
        // Condition future root choices on its newly observed hand, without indexing by hidden world hash/RNG.
        var text = new StringBuilder().Append(world.InteractionPlayerIndex).Append('|').Append(world.CurrentPhase)
            .Append('|').Append(world.TurnNumber).Append('|').Append(world.HasOpenResponseWindow).Append('|').Append(world.HasPendingTriggeredEffect);
        foreach (PlayerState player in world.Players)
        {
            text.Append('|').Append(player.Health).Append(':').Append(player.AvailableEnergy).Append(':').Append(player.MaxEnergy).Append(':').Append(player.Hand.Count);
            foreach (CardInstance card in player.MonsterZones.Concat(player.SpellTrapZones).Concat(new[] { player.FieldSpell }))
                text.Append('|').Append(card == null ? "empty" : card.IsFaceDown && player.PlayerIndex != rootPlayer ? "hidden"
                    : card.Definition.CardId + ":" + card.CurrentHealth + ":" + card.CurrentAttack + ":" + card.CurrentDefense + ":" + card.AllocatedEnergy + ":" + card.BattlePosition);
        }
        foreach (string card in world.Players[rootPlayer].Hand.Select(item => item.Definition.CardId).OrderBy(id => id, StringComparer.Ordinal)) text.Append('|').Append(card);
        return text.ToString();
    }
    private static double Evaluate(GameState world, int rootPlayer)
    {
        if (world.IsFinished) return world.WinnerPlayerIndex < 0 ? 0.5 : world.WinnerPlayerIndex == rootPlayer ? 1 : 0;
        double difference = PositionValue(world, rootPlayer) - PositionValue(world, 1 - rootPlayer);
        return 0.5 + 0.49 * Math.Tanh(difference / 6000.0);
    }
    private static double PositionValue(GameState world, int owner)
    {
        PlayerState player = world.Players[owner], enemy = world.Players[1 - owner];
        double value = player.Health + player.Hand.Count * 180 + player.AvailableEnergy * 45 + player.MaxEnergy * 90;
        foreach (CardInstance card in player.MonsterZones)
            if (card != null) value += 250 + card.CurrentHealth * 0.3 + card.CurrentAttack * 0.4 + card.CurrentDefense * 0.12;
        value += player.SpellTrapZones.Count(card => card != null) * 140 + (player.FieldSpell == null ? 0 : 220);
        value += HardStrategicEvaluation.PortfolioBonus(player.Hand, player.MonsterZones, enemy.MonsterZones,
            player.Deck.Count, player.AvailableEnergy, player.MaxEnergy, enemy.MaxEnergy,
            world.Config.NormalSummonEnergyCost, world.Config.MonsterZoneCount);
        return value;
    }
}
