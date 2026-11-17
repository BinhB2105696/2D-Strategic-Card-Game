#if UNITY_EDITOR || REPLAY_HEADLESS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

public static class HardBotVerification
{
    private static int checks, searches, fallbacks, applications;
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Card Game/Simulation/Verify Hard Bot Prototype")]
#endif
    public static void Verify()
    {
#if UNITY_EDITOR && !REPLAY_HEADLESS
        var catalog = UnityEditor.AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data/Cards" })
            .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<CardData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
            .Where(card => card != null).Select(CardDataConverter.ToDefinition).ToArray();
        VerifyCatalog(catalog);
#else
        VerifyCatalog(Catalog());
#endif
    }
    public static void VerifyCatalog(IReadOnlyList<CardDefinition> publicCatalog)
    {
        if (publicCatalog == null || publicCatalog.Count == 0) throw new ArgumentException("Verification requires public card definitions.");
        var catalog = publicCatalog.ToArray();
        checks = searches = fallbacks = applications = 0;
        var watch = Stopwatch.StartNew();
        Privacy(catalog);
        WinningAttack(catalog);
        Trajectory(921, catalog);
        Trajectory(922, catalog);
        string report = "Hard prototype PASS: " + checks + " checks, " + searches + " searched decisions, "
            + fallbacks + " explicit response/trigger fallbacks, " + applications + " rule applications, "
            + watch.Elapsed.TotalSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " s. Not a win-rate benchmark.";
#if UNITY_EDITOR && !REPLAY_HEADLESS
        UnityEngine.Debug.Log(report);
#else
        Console.WriteLine(report);
#endif
    }
    private static List<CardDefinition> Deck()
    {
        var cards = StrategicCardExpansionCatalog.Create().ToList();
        foreach (string id in new[] { "battle_herald", "bastion_keeper", "logistics_squire", "bloodfang_raider", "energy_wisp", "arcane_archivist" })
            cards.Add(cards.First(card => card.CardId == id));
        return cards;
    }
    private static CardDefinition[] Catalog()
    {
        // Explicit synthetic traps test hidden identity replacement; these are not playable card assets.
        return StrategicCardExpansionCatalog.Create().Concat(new[] { FixtureTrap("fixture.trap.a"), FixtureTrap("fixture.trap.b") }).ToArray();
    }
    private static CardDefinition FixtureTrap(string id) => new CardDefinition(id, id, CardCategory.Trap, "",
        MonsterAttribute.Wood, MonsterRank.Basic, MonsterType.Warrior, 0, 0, 0, 0, SpellType.Normal,
        TrapType.Normal, 0, CardSpeed.Speed2);
    private static void Privacy(CardDefinition[] catalog)
    {
        var deck = Deck();
        GameState source = GameState.CreateNewMatch(GameConfig.CreateDefault(), 921, deck, deck);
        Call(source, "SetCurrentPhase", TurnPhase.Main);
        int actor = source.CurrentPlayerIndex;
        var trap = new CardInstance(900003, 1 - actor, catalog.First(card => card.Category == CardCategory.Trap));
        trap.MoveToZone(CardZone.SpellTrapZone, true); trap.SetFaceDownInSpellTrapZone(source.TurnNumber);
        ((CardInstance[])Field(source.Players[1 - actor], "spellTrapZones"))[0] = trap;
        string before = SimulationStateHash.Capture(source);
        GameState altered = source.CreateSearchFork();
        var hiddenHand = (List<CardInstance>)Field(altered.Players[1 - actor], "hand");
        for (int i = 0; i < hiddenHand.Count; i++)
        {
            var card = new CardInstance(800000 + i, 1 - actor, catalog[(i + 4) % catalog.Length]);
            card.MoveToZone(CardZone.Hand, false); hiddenHand[i] = card;
        }
        foreach (PlayerState player in altered.Players) ((List<CardInstance>)Field(player, "deck")).Reverse();
        var otherTrap = new CardInstance(800099, 1 - actor, catalog.Last(card => card.Category == CardCategory.Trap));
        otherTrap.MoveToZone(CardZone.SpellTrapZone, true); otherTrap.SetFaceDownInSpellTrapZone(source.TurnNumber);
        ((CardInstance[])Field(altered.Players[1 - actor], "spellTrapZones"))[0] = otherTrap;
        altered.Random.NextUInt(); altered.Random.NextUInt();
        Check(before != SimulationStateHash.Capture(altered), "test changes true hidden state/RNG");
        GameState first = source.CreateInformationSetFork(actor, catalog, 701);
        GameState second = altered.CreateInformationSetFork(actor, catalog, 701);
        Check(SimulationStateHash.Capture(first) == SimulationStateHash.Capture(second), "same public information produces identical sampled world");
        Check(before == SimulationStateHash.Capture(source), "sampling leaves source/RNG untouched");
        GameState owned = source.CreateInformationSetFork(actor, catalog, 701, deck);
        GameState ownedAltered = altered.CreateInformationSetFork(actor, catalog, 701, deck);
        Check(SimulationStateHash.Capture(owned) == SimulationStateHash.Capture(ownedAltered), "known own deck still masks true shuffle/opponent cards/RNG");
        Func<IEnumerable<CardInstance>, string> composition = cards => string.Join("|", cards.Select(card => card.Definition.CardId).OrderBy(id => id, StringComparer.Ordinal));
        Check(composition(owned.Players[actor].Deck) == composition(source.Players[actor].Deck), "known own deck multiset retained, order resampled");
        bool rejected = false;
        try { source.CreateInformationSetFork(actor, catalog, 701, deck.Take(deck.Count - 1).ToArray()); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && before == SimulationStateHash.Capture(source), "inconsistent own deck knowledge fails closed without live mutation");
        foreach (PlayerState player in first.Players)
        {
            var cards = player.Hand.Concat(player.Deck).Concat(player.Graveyard)
                .Concat(player.MonsterZones).Concat(player.SpellTrapZones).Where(card => card != null).ToList();
            Check(cards.Select(card => card.InstanceId).Distinct().Count() == cards.Count, "sampled IDs unique");
            Check(cards.GroupBy(card => card.Definition.CardId).All(group => group.Count() <= group.First().Definition.MaxCopiesPerDeck), "public prior respects copy limits");
        }
        var legal = GameRules.GetLegalActions(source, actor);
        var alteredLegal = GameRules.GetLegalActions(altered, actor);
        var hard = new HardBot(catalog, 12, 4, 8, knownOwnStartingDeck: deck);
        hard.Observe(source, actor); GameAction choice = hard.ChooseAction(BotGameStateView.Create(source, actor), legal);
        Check(hard.LastIterations > 0 && hard.LastIterations <= 12 && hard.LastNodes > 1, "actual bounded tree search runs");
        applications += hard.LastRuleApplications; searches++;
        var same = new HardBot(catalog, 12, 4, 8, knownOwnStartingDeck: deck);
        same.Observe(altered, actor); GameAction other = same.ChooseAction(BotGameStateView.Create(altered, actor), alteredLegal);
        Check(Index(legal, choice) == Index(alteredLegal, other), "decision invariant to hidden identities/order and true RNG");
        Check(before == SimulationStateHash.Capture(source), "Hard never mutates source");
    }
    private static void WinningAttack(CardDefinition[] catalog)
    {
        var deck = Deck();
        GameState state = GameState.CreateNewMatch(GameConfig.CreateDefault(), 923, deck, deck);
        Call(state, "StartNextTurn"); Call(state, "SetCurrentPhase", TurnPhase.Battle);
        int actor = state.CurrentPlayerIndex;
        var attacker = new CardInstance(900001, actor, catalog.First(card => card.CardId == "bloodfang_raider"));
        Call(state.Players[actor], "PlaceMonsterFromHand", attacker, 0, BattlePosition.Attack);
        attacker.ResetTurnFlags(); attacker.AllocateEnergy(attacker.Definition.AttackEnergyRequirement);
        state.Players[1 - actor].TakeDamage(state.Players[1 - actor].Health - 1);
        var legal = GameRules.GetLegalActions(state, actor);
        var hard = new HardBot(catalog, 32, 4, 8);
        hard.Observe(state, actor); GameAction action = hard.ChooseAction(BotGameStateView.Create(state, actor), legal);
        Check(action.Type == GameActionType.Attack, "search chooses immediate winning attack");
        Check(GameRules.ApplyAction(state, action).Succeeded, "winning action accepted by rules");
        while (!state.IsFinished && state.HasOpenResponseWindow)
            Check(GameRules.ApplyAction(state, GameAction.PassResponse(state.InteractionPlayerIndex)).Succeeded, "winning attack resolves through legal response passes");
        Check(state.IsFinished && state.WinnerPlayerIndex == actor, "terminal outcome follows real rules");
    }
    private static void Trajectory(uint seed, CardDefinition[] catalog)
    {
        var deck = Deck();
        GameState state = GameState.CreateNewMatch(GameConfig.CreateDefault(), seed, deck, deck);
        var hard = new HardBot(catalog, 8, 4, 8, knownOwnStartingDeck: deck);
        var normal = new NormalBot();
        for (int step = 0; step < 100 && !state.IsFinished; step++)
        {
            GameAction action = SimulationEngine.AutomaticAction(state);
            if (action == null)
            {
                int actor = state.InteractionPlayerIndex;
                var legal = GameRules.GetLegalActions(state, actor);
                string before = SimulationStateHash.Capture(state);
                if (actor == 0)
                {
                    hard.Observe(state, actor); action = hard.ChooseAction(BotGameStateView.Create(state, actor), legal);
                    applications += hard.LastRuleApplications;
                    if (hard.LastIterations > 0) searches++;
                    if (hard.LastFallbackReason != null)
                    {
                        Check(hard.LastFallbackReason == "response-or-trigger-root", "no unexpected sampler fallback: " + hard.LastFallbackReason);
                        fallbacks++;
                    }
                    Check(hard.LastIterations <= 8 && hard.LastNodes <= 9, "iteration/node budget respected");
                }
                else { normal.Observe(state, actor); action = normal.ChooseAction(BotGameStateView.Create(state, actor), legal); }
                Check(Index(legal, action) >= 0, "bot returns original supplied action object");
                Check(before == SimulationStateHash.Capture(state), "decision leaves complete live state unchanged");
            }
            Check(GameRules.ApplyAction(state, action).Succeeded, "selected move obeys rules");
        }
        Check(searches > 5, "real trajectories exercise search");
    }
    private static int Index(IReadOnlyList<GameAction> legal, GameAction action)
    { for (int i = 0; i < legal.Count; i++) if (ReferenceEquals(legal[i], action)) return i; return -1; }
    private static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    private static object Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args);
    private static void Check(bool condition, string reason)
    { checks++; if (!condition) throw new InvalidOperationException("Hard prototype FAIL: " + reason); }
}
#endif
