#if UNITY_EDITOR || REPLAY_HEADLESS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class GameStateForkVerification
{
    private static int checks;
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Card Game/Simulation/Verify Search State Fork")]
#endif
    public static void Verify()
    {
        checks = 0;
        ScalarCloneContract();
        MutableGraph();
        LegalTrajectories(811);
        LegalTrajectories(812);
#if UNITY_EDITOR && !REPLAY_HEADLESS
        UnityEngine.Debug.Log("Search fork PASS: " + checks + " checks; isolation, RNG, modifiers/chain and legal trajectory equivalence.");
#else
        Console.WriteLine("Search fork PASS: " + checks + " checks; isolation, RNG, modifiers/chain and legal trajectory equivalence.");
#endif
    }

    private static void ScalarCloneContract()
    {
        // Memberwise copies remain safe only while these types have no mutable reference fields.
        foreach (Type type in new[] { typeof(CardInstance), typeof(SeededGameRandom) })
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                Check(field.FieldType.IsValueType || field.FieldType == typeof(string) || field.FieldType == typeof(CardDefinition),
                    "new mutable field needs a deep copy: " + type.Name + "." + field.Name);
    }

    private static List<CardDefinition> Deck()
    {
        var deck = StrategicCardExpansionCatalog.Create().ToList();
        foreach (string id in new[] { "battle_herald", "bastion_keeper", "logistics_squire", "bloodfang_raider", "energy_wisp", "arcane_archivist" })
            deck.Add(deck.First(card => card.CardId == id));
        return deck;
    }

    private static void MutableGraph()
    {
        var deck = Deck();
        GameState source = GameState.CreateNewMatch(GameConfig.CreateDefault(), 41, deck, deck);
        Call(source, "StartNextTurn"); Call(source, "SetCurrentPhase", TurnPhase.Main);
        int actor = source.CurrentPlayerIndex;
        CardInstance card = Place(source, actor, 0, 900001, deck.First(item => item.CardId == "scar_duelist"));
        CardInstance enemy = Place(source, 1 - actor, 0, 900002, deck.First(item => item.CardId == "aegis_sentinel"));
        card.ModifyMaximumHealth(200); card.ReceiveDamage(350); card.ModifyAttack(-5000); card.AllocateEnergy(2);
        source.Players[actor].TakeDamage(600);
        source.Random.NextUInt(); source.Random.NextUInt();
        Call(source, "CaptureMainStart"); Call(source, "AddRetaliationMark", card, enemy, 300);
        var modifier = new StatModifierApplication(card.InstanceId, card, 100, 200, source.TurnNumber + 1, 50);
        modifier.Apply(); ((List<StatModifierApplication>)Field(source, "statModifiers").GetValue(source)).Add(modifier);
        ((Dictionary<string, int>)Field(source, "effectUses").GetValue(source)).Add("fixture.used", 2);
        ((HashSet<int>)Field(source.Players[actor], "skippedDrawTurns").GetValue(source.Players[actor])).Add(source.TurnNumber + 2);
        EffectDefinition effect = deck.First(item => item.CardId == "field_medic").Effects[0];
        var pending = new PendingTriggeredEffect(actor, card.InstanceId, effect, new[] { card.InstanceId });
        typeof(GameState).GetProperty("PendingTriggeredEffect").GetSetMethod(true).Invoke(source, new object[] { pending });
        ((List<PendingTriggeredEffect>)Field(source, "queuedTriggeredEffects").GetValue(source)).Add(pending);
        var chain = new ChainState(ResponseWindowType.AttackDeclaration, actor, new PendingAttack(actor, card.InstanceId, enemy.InstanceId));
        chain.AddLink(new ChainLink(actor, card.InstanceId, 0, CardSpeed.Speed1, effect, new[] { enemy.InstanceId }));
        chain.AddLink(new ChainLink(1 - actor, enemy.InstanceId, 0, CardSpeed.Speed2, effect, new[] { card.InstanceId }));
        chain.Links[0].Negate(); chain.Pass(); chain.BeginResolvingLink(1);
        typeof(GameState).GetProperty("ActiveChain").GetSetMethod(true).Invoke(source, new object[] { chain });
        string hash = SimulationStateHash.Capture(source);
        GameState fork = source.CreateSearchFork();
        Equal(hash, SimulationStateHash.Capture(fork), "exact copy includes latent state/aliasing");
        Check(!ReferenceEquals(source, fork) && !ReferenceEquals(source.Players[actor], fork.Players[actor]), "root/player copied");
        CardInstance copy = fork.Players[actor].MonsterZones[0];
        Check(!ReferenceEquals(card, copy) && ReferenceEquals(card.Definition, copy.Definition), "instance isolated, immutable definition shared");
        var copiedModifiers = (List<StatModifierApplication>)Field(fork, "statModifiers").GetValue(fork);
        Check(ReferenceEquals(copiedModifiers[0].Target, copy), "modifier target points into fork");
        Check(ReferenceEquals(fork.PendingTriggeredEffect, ((List<PendingTriggeredEffect>)Field(fork, "queuedTriggeredEffects").GetValue(fork))[0]), "pending alias preserved inside fork");
        Check(fork.Random.NextUInt() == source.CreateSearchFork().Random.NextUInt(), "RNG stream copied exactly");
        Check(source.CreateSearchFork(1234).Random.NextUInt() == new SeededGameRandom(1234).NextUInt(), "independent simulation seed");
        copiedModifiers[0].Remove();
        fork.ActiveChain.Links[1].Negate(); fork.ActiveChain.Pass();
        ((long[])fork.PendingTriggeredEffect.CandidateInstanceIds)[0] = enemy.InstanceId;
        fork.Players[actor].DrawCards(1); fork.Players[actor].TrySpendEnergy(1);
        ((Dictionary<string, int>)Field(fork, "effectUses").GetValue(fork))["fixture.used"] = 7;
        ((HashSet<int>)Field(fork.Players[actor], "skippedDrawTurns").GetValue(fork.Players[actor])).Add(source.TurnNumber + 4);
        Call(fork, "ConsumeRetaliationReduction", enemy.InstanceId, card.InstanceId, 100);
        Check(source.TryGetRetaliationMark(card.InstanceId, enemy.InstanceId, out int remaining, out int expires) && remaining == 300, "source mark not consumed");
        Equal(hash, SimulationStateHash.Capture(source), "all fork mutations leave source untouched");
    }

    private static void LegalTrajectories(uint seed)
    {
        var deck = Deck();
        GameState source = GameState.CreateNewMatch(GameConfig.CreateDefault(), seed, deck, deck);
        var bots = new[] { new NormalBot(), new NormalBot() };
        int decisions = 0;
        for (int step = 0; step < 120 && !source.IsFinished; step++)
        {
            string before = SimulationStateHash.Capture(source);
            GameState fork = source.CreateSearchFork();
            Equal(before, SimulationStateHash.Capture(fork), "trajectory copy at step " + step);
            GameAction action = SimulationEngine.AutomaticAction(source);
            if (action == null)
            {
                int actor = source.InteractionPlayerIndex;
                var legal = GameRules.GetLegalActions(source, actor);
                Check(legal.Count > 0, "interactive state has legal choices");
                bots[actor].Observe(source, actor);
                action = bots[actor].ChooseAction(BotGameStateView.Create(source, actor), legal);
                var rejected = GameRules.ApplyAction(fork, GameAction.AdvancePhase(1 - actor));
                Check(!rejected.Succeeded, "wrong-player action rejected");
                Equal(before, SimulationStateHash.Capture(fork), "failed validation does not mutate fork/pay costs");
                decisions++;
            }
            ActionResult branch = GameRules.ApplyAction(fork, action);
            Check(branch.Succeeded, "legal branch action succeeds");
            Equal(before, SimulationStateHash.Capture(source), "branch leaves live game unchanged");
            ActionResult actual = GameRules.ApplyAction(source, action);
            Check(actual.Succeeded, "same live action succeeds");
            Equal(SimulationStateHash.Capture(source), SimulationStateHash.Capture(fork), "same transition/chance result after fork");
        }
        Check(decisions > 10, "trajectory exercises actual bot decisions");
    }

    private static CardInstance Place(GameState state, int owner, int slot, long id, CardDefinition definition)
    {
        var card = new CardInstance(id, owner, definition);
        Call(state.Players[owner], "PlaceMonsterFromHand", card, slot, BattlePosition.Attack);
        card.ResetTurnFlags(); return card;
    }
    private static FieldInfo Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(value.GetType().Name, name);
    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Equal(string left, string right, string reason) => Check(left == right, reason);
    private static void Check(bool condition, string reason)
    { checks++; if (!condition) throw new InvalidOperationException("Search fork FAIL: " + reason); }
}
#endif
