using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

// Host-only adapter. All gameplay mutations go through the existing GameSession.
public sealed class PvpDuelAuthority
{
    public const int PageSize = 6;
    private readonly Dictionary<string, CardDefinition> catalog;
    private readonly Func<string[], string> validateDeck;
    private readonly string fingerprint;
    private readonly uint seed;
    private readonly bool[] ready = new bool[2];
    private readonly string[][] decks = new string[2][];
    private readonly string[] names = { "Player 1", "Player 2" };
    private readonly int[] avatars = new int[2];
    private readonly List<string> log = new List<string>();
    private GameSession session;
    private int revision, winner = -2;
    private bool ended;
    private string reason = "";
    private readonly string matchId = Guid.NewGuid().ToString("N");
    private readonly Func<double> clock;
    private readonly double[] seconds = { 120, 120 };
    private readonly bool[] boardReady = new bool[2];
    private bool clockStarted;
    private double lastClock;
    private int clockTurn, actionSequence, lastActor;
    private string lastAction = "";
    private long lastSource, lastTarget;

    public PvpDuelAuthority(IEnumerable<CardDefinition> cards, string fingerprint,
        uint seed, Func<string[], string> validateDeck, Func<double> clock = null)
    {
        catalog = cards.ToDictionary(card => card.CardId, StringComparer.Ordinal);
        this.fingerprint = fingerprint; this.seed = seed; this.validateDeck = validateDeck;
        this.clock = clock ?? (() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
    }

    public string Ready(int actor, string hash, string name, string[] deck, int avatar = 0)
    {
        if (actor < 0 || actor > 1 || ended || session != null) return "The match has already started or ended.";
        if (hash != fingerprint) return "Card/rule versions differ. Both players need the same game build.";
        if (ready[actor]) return "Already ready.";
        if (deck == null || deck.Length > GameConfig.CreateDefault().MaximumDeckSize ||
            deck.Any(id => id == null || !catalog.ContainsKey(id))) return "Invalid deck.";
        string error = validateDeck(deck);
        if (!string.IsNullOrEmpty(error)) return error;
        names[actor] = CleanName(name);
        avatars[actor] = Math.Max(0, Math.Min(31, avatar));
        decks[actor] = (string[])deck.Clone();
        ready[actor] = true;
        revision++;
        if (ready[0] && ready[1])
        {
            session = GameSession.Create(GameConfig.CreateDefault(), seed,
                decks[0].Select(id => catalog[id]).ToArray(), decks[1].Select(id => catalog[id]).ToArray());
            log.Add("Match started. Player 1 goes first.");
            PumpAutomatic();
        }
        return null;
    }

    public string Act(int actor, int expectedRevision, int index)
    {
        Tick();
        if (actor < 0 || actor > 1 || session == null || ended) return "No active match.";
        if (!clockStarted) return "Waiting for both duel boards to load.";
        if (expectedRevision != revision) return "The board changed. Choose an action again.";
        if (session.State.InteractionPlayerIndex != actor) return "Waiting for the other player.";
        var legal = GameRules.GetLegalActions(session.State, actor);
        if (index < 0 || index >= legal.Count) return "Invalid action selection.";
        ActionResult result = session.TryApply(legal[index]);
        if (!result.Succeeded) return result.ErrorMessage;
        revision++;
        actionSequence++; lastAction = legal[index].Type.ToString(); lastActor = actor;
        lastSource = legal[index].SourceInstanceId; lastTarget = legal[index].TargetInstanceId;
        // No hidden card identities/targets are exposed in this public summary.
        log.Add("Player " + (actor + 1) + ": " + legal[index].Type);
        PumpAutomatic();
        return null;
    }

    public void BoardReady(int actor)
    {
        if (actor < 0 || actor > 1 || session == null || ended) return;
        boardReady[actor] = true;
        if (!clockStarted && boardReady[0] && boardReady[1])
        { clockStarted = true; lastClock = clock(); clockTurn = session.State.TurnNumber; }
    }

    public void Forfeit(int actor, string cause)
    {
        if (actor < 0 || actor > 1 || session == null || ended) return;
        ended = true; winner = 1 - actor; reason = cause; revision++;
        log.Add("Player " + (actor + 1) + " left the match.");
    }

    public bool Tick()
    {
        if (!clockStarted || session == null || ended) return false;
        double now = clock();
        int actor = session.State.InteractionPlayerIndex;
        seconds[actor] = Math.Max(0, seconds[actor] - Math.Max(0, now - lastClock));
        lastClock = now;
        bool changed = false;
        for (int guard = 0; guard < 128 && !ended; guard++)
        {
            actor = session.State.InteractionPlayerIndex;
            if (seconds[actor] > 0) break;
            var legal = GameRules.GetLegalActions(session.State, actor);
            GameAction automatic = legal.FirstOrDefault(action => action.Type == GameActionType.PassResponse) ??
                legal.FirstOrDefault(action => action.Type == GameActionType.DeclineTriggeredEffect) ??
                legal.FirstOrDefault(action => action.Type == GameActionType.EnterEndPhase) ??
                legal.FirstOrDefault(action => action.Type == GameActionType.EndTurn) ??
                legal.FirstOrDefault(action => action.Type == GameActionType.AdvancePhase) ??
                legal.FirstOrDefault(action => action.Type == GameActionType.ResolveTriggeredEffect);
            if (automatic == null) throw new InvalidOperationException("No legal timeout progression is available.");
            ActionResult result = session.TryApply(automatic);
            if (!result.Succeeded) throw new InvalidOperationException(result.ErrorMessage);
            revision++; changed = true;
            log.Add("Player " + (actor + 1) + " time expired: " + automatic.Type);
            PumpAutomatic();
        }
        return changed;
    }

    public void Surrender(int actor)
    {
        if (actor < 0 || actor > 1 || session == null || ended) return;
        ended = true; winner = 1 - actor; reason = "Surrender"; revision++;
        log.Add("Player " + (actor + 1) + " surrendered.");
    }

    public void Cancel()
    {
        if (ended) return;
        ended = true; winner = -2; reason = "Disconnected"; revision++;
    }

    private void PumpAutomatic()
    {
        for (int guard = 0; guard < 128; guard++)
        {
            GameState state = session.State;
            if (clockStarted && state.TurnNumber != clockTurn)
            { clockTurn = state.TurnNumber; seconds[state.CurrentPlayerIndex] = 120; }
            if (state.IsFinished)
            { ended = true; winner = state.WinnerPlayerIndex; reason = "Rules"; return; }
            if (state.HasOpenResponseWindow || state.HasPendingTriggeredEffect) return;
            int player = state.InteractionPlayerIndex;
            GameAction automatic = state.CurrentPhase == TurnPhase.Standby ? GameAction.AdvancePhase(player) :
                state.CurrentPhase == TurnPhase.Recovery ?
                (state.CurrentRecoveryStep == RecoveryStep.Draw ? GameAction.ResolveDrawStep(player) :
                    GameAction.ResolveEnergyRefreshStep(player)) : null;
            if (automatic == null) return;
            ActionResult result = session.TryApply(automatic);
            if (!result.Succeeded) throw new InvalidOperationException(result.ErrorMessage);
            revision++;
        }
        throw new InvalidOperationException("Automatic turn progression exceeded its safety bound.");
    }

    public PvpMatchView View(int seat, int page = 0, string notice = null, PvpActionQuery query = null)
    {
        if (seat < 0 || seat > 1) throw new ArgumentOutOfRangeException(nameof(seat));
        var view = new PvpMatchView
        {
            matchId = matchId, revision = revision, seat = seat, names = (string[])names.Clone(), avatars = (int[])avatars.Clone(),
            started = session != null, finished = ended, myReady = ready[seat], opponentReady = ready[1 - seat],
            notice = notice ?? "", reason = reason, log = log.Skip(Math.Max(0, log.Count - 6)).ToArray(),
            outcome = !ended ? "" : winner == -2 ? "Cancelled" : winner < 0 ? "Draw" : winner == seat ? "Win" : "Loss",
            options = new PvpActionOption[0], players = new PvpPlayerView[0]
        };
        view.clockStarted = clockStarted; view.seconds = seconds.Select(value => (float)value).ToArray();
        view.timerActor = session != null ? session.State.InteractionPlayerIndex : 0;
        view.actionSequence = actionSequence; view.lastAction = lastAction; view.lastSource = lastSource;
        view.lastTarget = lastTarget; view.lastActor = lastActor; view.logSequence = log.Count;
        view.query = query ?? new PvpActionQuery(); view.choices = new PvpActionChoices();
        if (session == null) return view;
        BotGameStateView visible = BotGameStateView.Create(session.State, seat);
        view.turn = visible.TurnNumber; view.phase = visible.CurrentPhase.ToString();
        view.activePlayer = visible.CurrentPlayerIndex; view.interactionPlayer = visible.InteractionPlayerIndex;
        view.response = visible.HasOpenResponseWindow; view.trigger = visible.HasPendingTriggeredEffect;
        view.players = visible.Players.Select(Player).ToArray();
        // Both Graveyards are public by the existing rules; the bot view omits the opponent's.
        for (int player = 0; player < view.players.Length; player++)
            view.players[player].grave = session.State.Players[player].Graveyard.Reverse()
                .Select(card => Card(new BotCardView(card, true))).ToArray();
        var legal = ended ? new GameAction[0] : GameRules.GetLegalActions(session.State, seat);
        var indexed = legal.Select((action, index) => new { action, index });
        view.choices.transitions = legal.Where(action => action.SourceInstanceId == 0)
            .Select(action => action.Type.ToString()).Distinct().ToArray();
        var source = indexed.Where(item => view.query.source == 0 || item.action.SourceInstanceId == view.query.source).ToArray();
        view.choices.types = source.Select(item => item.action.Type.ToString()).Distinct().ToArray();
        view.choices.sources = legal.Select(action => action.SourceInstanceId).Where(id => id > 0).Distinct().ToArray();
        var filtered = source.Where(item => Matches(item.action, view.query)).ToArray();
        view.choices.positions = filtered.Where(item => item.action.BattlePosition != BattlePosition.None)
            .Select(item => item.action.BattlePosition.ToString()).Distinct().ToArray();
        view.choices.zones = filtered.Select(item => item.action.ZoneIndex).Where(index => index >= 0).Distinct().ToArray();
        view.choices.amounts = filtered.Select(item => item.action.Amount).Where(amount => amount > 0).Distinct().ToArray();
        view.choices.targets = filtered.SelectMany(item => item.action.TargetInstanceIds).Distinct().ToArray();
        view.choices.tributes = filtered.SelectMany(item => item.action.TributeInstanceIds).Distinct().ToArray();
        view.choices.targetCount = filtered.Length == 0 ? 0 : filtered.Min(item => item.action.TargetInstanceIds.Count);
        view.choices.tributeCount = filtered.Length == 0 ? 0 : filtered.Min(item => item.action.TributeInstanceIds.Count);
        view.optionTotal = filtered.Length;
        view.optionPage = Math.Max(0, Math.Min(page, Math.Max(0, (filtered.Length - 1) / PageSize)));
        view.options = filtered.Skip(view.optionPage * PageSize).Take(PageSize)
            .Select(item => new PvpActionOption
            {
                index = item.index, text = Describe(item.action, visible),
                type = item.action.Type.ToString(), source = item.action.SourceInstanceId
            }).ToArray();
        return view;
    }

    private static bool Matches(GameAction action, PvpActionQuery query) =>
        (string.IsNullOrEmpty(query.type) || action.Type.ToString() == query.type) &&
        (string.IsNullOrEmpty(query.position) || action.BattlePosition.ToString() == query.position) &&
        (query.zone < 0 || action.ZoneIndex == query.zone) && (query.amount < 0 || action.Amount == query.amount) &&
        (query.targets == null || query.targets.All(id => action.TargetInstanceIds.Contains(id))) &&
        (query.tributes == null || query.tributes.All(id => action.TributeInstanceIds.Contains(id)));

    private static PvpPlayerView Player(BotPlayerView player) => new PvpPlayerView
    {
        health = player.Health, energy = player.AvailableEnergy, maxEnergy = player.MaxEnergy,
        deckCount = player.DeckCount, handCount = player.HandCount, graveCount = player.GraveyardCount,
        hand = player.Hand.Select(Card).ToArray(), monsters = player.MonsterZones.Select(Card).ToArray(),
        spells = player.SpellTrapZones.Select(Card).ToArray(), grave = player.Graveyard.Select(Card).ToArray(),
        field = Card(player.FieldSpell)
    };

    private static PvpPublicCard Card(BotCardView card) => card == null ? null : new PvpPublicCard
    {
        known = card.IsKnown, faceDown = card.IsFaceDown,
        cardId = card.IsKnown ? card.CardId : "", name = card.CardName,
        instanceId = card.InstanceId, monster = card.IsKnown && card.Category == CardCategory.Monster,
        position = card.BattlePosition.ToString(), hp = card.CurrentHealth, maxHp = card.MaximumHealth,
        atk = card.CurrentAttack, def = card.CurrentDefense, energy = card.AllocatedEnergy,
        attackRequirement = card.AttackEnergyRequirement
    };

    private static string Describe(GameAction action, BotGameStateView view)
    {
        string CardName(long id) => view.FindCard(id)?.CardName ?? "card";
        string text = action.Type.ToString();
        if (action.SourceInstanceId != 0) text += " - " + CardName(action.SourceInstanceId);
        if (action.ZoneIndex >= 0) text += " / zone " + (action.ZoneIndex + 1);
        if (action.BattlePosition != BattlePosition.None) text += " / " + action.BattlePosition;
        if (action.Amount != 0) text += " / " + action.Amount + " Energy";
        if (action.TargetInstanceIds.Count > 0)
            text += " -> " + string.Join(", ", action.TargetInstanceIds.Select(CardName));
        if (action.TributeInstanceIds.Count > 0)
            text += " / tribute " + string.Join(", ", action.TributeInstanceIds.Select(CardName));
        return text.Length <= 700 ? text : text.Substring(0, 700);
    }

    public static string CleanName(string name)
    {
        string clean = new string((name ?? "Player").Where(character =>
            !char.IsControl(character) && character != '<' && character != '>').Take(24).ToArray()).Trim();
        return clean.Length == 0 ? "Player" : clean;
    }
}
