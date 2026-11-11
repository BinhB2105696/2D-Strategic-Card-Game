using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// Pure checks shared by the Editor diagnostic and standalone development check; not shipped in Players.
public static class PvpAuthorityChecks
{
    private sealed class FixtureModifier : IContinuousModifier
    {
        private readonly int amount;
        public FixtureModifier(int amount) { this.amount = amount; }
        public int ModifyHealth(CardInstance source, CardInstance target) => 0;
        public int ModifyAttack(CardInstance source, CardInstance target) => amount;
        public int ModifyDefense(CardInstance source, CardInstance target) => 0;
    }

    public static int Run()
    {
        int checks = 0;
        void Check(bool success, string message)
        { if (!success) throw new InvalidOperationException(message); checks++; }
        CardDefinition Card(int index, int attack = 300) => new CardDefinition("pvp_fixture_" + index,
            "Fixture " + index, index == 9 ? CardCategory.Trap : CardCategory.Monster, "",
            default, default, default, 600, attack, 200, 1, default, default, 0,
            index == 9 ? CardSpeed.Speed2 : CardSpeed.Speed1);
        var cards = Enumerable.Range(0, 10).Select(index => Card(index)).ToArray();
        string[] deck = cards.SelectMany(card => Enumerable.Repeat(card.CardId, 3)).ToArray();
        string fingerprint = PvpCatalogFingerprint.Compute(cards);
        Check(fingerprint == PvpCatalogFingerprint.Compute(cards.Reverse()), "Unstable catalog ordering.");
        Check(fingerprint != PvpCatalogFingerprint.Compute(cards.Select((card, index) => index == 0 ? Card(0, 301) : card)),
            "Stat changes did not invalidate compatibility.");
        CardDefinition WithModifier(int amount) => new CardDefinition("modifier", "Modifier", CardCategory.Spell, "",
            default, default, default, 0, 0, 0, 0, SpellType.Equip, default, 0, CardSpeed.Speed1,
            new[] { new EffectDefinition("boost", EffectTriggerType.ManualActivation, EffectKind.Continuous,
                EffectPhaseMask.Any, EffectTurnScope.EitherTurn, EffectActivationWindow.OpenGameState,
                EffectActivationSource.Standard, null, null, EffectTargetType.OwnMonster, 1, null,
                continuousModifiers: new IContinuousModifier[] { new FixtureModifier(amount) }) });
        Check(PvpCatalogFingerprint.Compute(new[] { WithModifier(100) }) !=
            PvpCatalogFingerprint.Compute(new[] { WithModifier(200) }), "Private modifier amount was omitted from compatibility.");
        string Validate(string[] ids) => ids.Length == 30 && ids.GroupBy(id => id).All(group => group.Count() <= 3)
            ? null : "Invalid fixture deck.";
        PvpDuelAuthority New(uint seed) => new PvpDuelAuthority(cards, fingerprint, seed, Validate);
        var authority = New(42);
        Check(authority.Ready(0, "wrong", "Player", deck) != null, "Version mismatch accepted.");
        Check(authority.Ready(0, fingerprint, "Player", new[] { "unknown" }) != null, "Unknown card accepted.");
        Check(authority.Ready(2, fingerprint, "Player", deck) != null, "Invalid actor accepted.");
        Check(authority.Ready(0, fingerprint, "<b>A</b>", deck) == null && !authority.View(0).started,
            "Match started before both players were ready.");
        Check(authority.Ready(1, fingerprint, "B", deck) == null, "Valid ready rejected.");
        authority.BoardReady(0); authority.BoardReady(1);
        PvpMatchView first = authority.View(0);
        Check(first.started && first.phase == TurnPhase.Main.ToString() && first.turn == 1,
            "Opening automatic steps did not reach Main.");
        Check(first.players[0].hand.Length == first.players[0].handCount && first.players[1].hand.Length == 0,
            "Perspective leaked the opposing hand.");
        Check(!first.names[0].Contains("<"), "Untrusted name markup was retained.");
        Check(authority.Act(1, first.revision, 0) != null, "Wrong-turn action accepted.");
        Check(authority.Act(0, first.revision, -1) != null, "Invalid action index accepted.");
        Check(first.options.Length > 0 && authority.Act(0, first.revision, first.options[0].index) == null,
            "Legal action rejected.");
        int after = authority.View(0).revision;
        Check(authority.Act(0, first.revision, first.options[0].index) != null && authority.View(0).revision == after,
            "Duplicate/stale request changed the state.");
        authority.Surrender(0);
        Check(authority.View(0).outcome == "Loss" && authority.View(1).outcome == "Win", "Surrender outcome is wrong.");
        authority.Cancel();
        Check(authority.View(1).outcome == "Win", "Disconnect overwrote a completed result.");
        var disconnected = New(43);
        disconnected.Ready(0, fingerprint, "A", deck); disconnected.Ready(1, fingerprint, "B", deck);
        disconnected.Forfeit(1, "Disconnected");
        Check(disconnected.View(0).outcome == "Win" && disconnected.View(1).outcome == "Loss",
            "Disconnected player was not given an immediate defeat.");
        bool testedSet = false;
        for (uint seed = 1; seed <= 32 && !testedSet; seed++)
        {
            var trial = New(seed);
            trial.Ready(0, fingerprint, "A", deck); trial.Ready(1, fingerprint, "B", deck);
            trial.BoardReady(0); trial.BoardReady(1);
            PvpMatchView view = trial.View(0);
            for (int page = 0; page * PvpDuelAuthority.PageSize < view.optionTotal; page++)
            {
                var set = trial.View(0, page).options.FirstOrDefault(option => option.text.StartsWith("SetTrap"));
                if (set == null) continue;
                Check(trial.Act(0, view.revision, set.index) == null, "SetTrap failed through the host adapter.");
                PvpPublicCard own = trial.View(0).players[0].spells.First(card => card != null);
                PvpPublicCard enemy = trial.View(1).players[0].spells.First(card => card != null);
                Check(own.known && own.faceDown && !enemy.known && enemy.faceDown &&
                    enemy.cardId == "" && enemy.hp == 0 && enemy.atk == 0,
                    "Opponent face-down identity or stats leaked.");
                testedSet = true; break;
            }
        }
        Check(testedSet, "No face-down privacy case was exercised.");
        var complete = New(123);
        complete.Ready(0, fingerprint, "A", deck); complete.Ready(1, fingerprint, "B", deck);
        complete.BoardReady(0); complete.BoardReady(1);
        // Read only our adapter's session for the test policy; no private Core mutation.
        var hostSession = (GameSession)typeof(PvpDuelAuthority)
            .GetField("session", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(complete);
        var bot = new EasyBot();
        bool sawPublicGrave = false;
        for (int step = 0; step < 2000 && !complete.View(0).finished; step++)
        {
            int actor = hostSession.State.InteractionPlayerIndex;
            var legal = GameRules.GetLegalActions(hostSession.State, actor).ToList();
            GameAction selected = bot.ChooseAction(BotGameStateView.Create(hostSession.State, actor), legal);
            int index = legal.IndexOf(selected);
            string error = complete.Act(actor, complete.View(actor).revision, index);
            if (error != null) throw new InvalidOperationException("Full-match host adapter rejected a legal bot action: " + error);
            PvpMatchView perspective = complete.View(actor);
            if (perspective.players[1 - actor].hand.Length != 0)
                throw new InvalidOperationException("Opposing hand leaked during full match.");
            for (int player = 0; player < 2; player++)
            {
                var actual = hostSession.State.Players[player].Graveyard;
                if (actual.Count == 0) continue;
                if (!perspective.players[player].grave.Select(card => card.cardId)
                    .SequenceEqual(actual.Reverse().Select(card => card.Definition.CardId)))
                    throw new InvalidOperationException("Public Graveyard contents/order differ from the rules.");
                sawPublicGrave = true;
            }
        }
        Check(complete.View(0).finished && hostSession.State.IsFinished, "Host adapter did not finish a complete rules-driven match.");
        Check(sawPublicGrave, "Public Graveyard was not exercised.");
        string firstOutcome = complete.View(0).outcome, secondOutcome = complete.View(1).outcome;
        Check((firstOutcome == "Win" && secondOutcome == "Loss") ||
            (firstOutcome == "Loss" && secondOutcome == "Win") || (firstOutcome == "Draw" && secondOutcome == "Draw"),
            "Rules-driven terminal views disagree.");
        double now = 100;
        var timed = new PvpDuelAuthority(cards, fingerprint, 17, Validate, () => now);
        timed.Ready(0, fingerprint, "A", deck); timed.Ready(1, fingerprint, "B", deck);
        now += 500; timed.Tick();
        Check(!timed.View(0).clockStarted && timed.View(0).turn == 1, "Clock ran before boards finished loading.");
        timed.BoardReady(0); timed.Tick();
        Check(!timed.View(0).clockStarted, "Clock started with only one board loaded.");
        timed.BoardReady(1);
        int beforeTimeout = timed.View(0).revision;
        now += 119; timed.Tick();
        Check(timed.View(0).turn == 1 && Math.Abs(timed.View(0).seconds[0] - 1) < .01, "120-second budget is inaccurate.");
        now += 2; timed.Tick();
        Check(timed.View(0).turn == 2 && timed.View(0).activePlayer == 1 && timed.View(1).seconds[1] == 120,
            "Timeout did not legally end the turn or reset the next player's budget.");
        Check(timed.Act(0, beforeTimeout, 0) != null, "Late action was accepted after timeout.");
        PvpMatchView endOptions = timed.View(1, 0, null, new PvpActionQuery { type = "EndTurn" });
        Check(endOptions.options.All(option => option.type == "EndTurn"), "Action filtering changed option types.");
        var filtered = timed.View(1, 0, null, new PvpActionQuery { type = "EnterEndPhase" });
        if (filtered.options.Length > 0)
            Check(timed.Act(1, filtered.revision, filtered.options[0].index) == null, "Filtered action lost its original legal-action index.");
        timed.Forfeit(0, "Disconnected");
        Check(timed.View(1).outcome == "Win" && timed.View(0).outcome == "Loss", "Host-side disconnect outcome is wrong.");
        return checks;
    }
}
