using System;
using System.Collections.Generic;

// Match-local journal. Freeze both perspectives at event time, never reconstruct
// old lines from the latest board or the animation queue.
public sealed class DuelLogModel
{
    private sealed class Card
    {
        public string Name;
        public int Owner, Hp, MaxHp, Attack, Defense, Energy;
        public bool Hidden;
    }
    private readonly Dictionary<long, Card> cards = new Dictionary<long, Card>();
    private readonly List<string>[] lines = { new List<string>(), new List<string>() };
    private readonly IReadOnlyList<string>[] views;
    private readonly int[] lp = new int[2], energy = new int[2];
    private int turn, sequence;
    private TurnPhase phase;
    public int Revision { get; private set; }

    public DuelLogModel(GameState state)
    {
        views = new[] { lines[0].AsReadOnly(), lines[1].AsReadOnly() };
        Capture(state);
        Add("DUEL START — P" + (state.CurrentPlayerIndex + 1) + " goes first. Opening hands: P1 " +
            state.Players[0].Hand.Count + ", P2 " + state.Players[1].Hand.Count + ".");
    }

    public IReadOnlyList<string> Lines(int viewer) => views[viewer];

    public void Capture(GameState state)
    {
        turn = state.TurnNumber; phase = state.CurrentPhase;
        foreach (PlayerState player in state.Players)
        {
            lp[player.PlayerIndex] = player.Health;
            energy[player.PlayerIndex] = player.AvailableEnergy;
            Copy(player.Hand); Copy(player.MonsterZones); Copy(player.SpellTrapZones); Copy(player.Graveyard);
            if (player.FieldSpell != null) CopyCard(player.FieldSpell);
        }
    }

    private void Copy(IReadOnlyList<CardInstance> source)
    {
        foreach (CardInstance card in source) if (card != null) CopyCard(card);
    }

    private void CopyCard(CardInstance card)
    {
        cards[card.InstanceId] = new Card { Name = card.Definition.CardName,
            Owner = card.OwnerPlayerIndex, Hidden = card.IsFaceDown || card.Zone == CardZone.Hand,
            Hp = card.CurrentHealth, MaxHp = card.MaximumHealth, Attack = card.CurrentAttack,
            Defense = card.CurrentDefense, Energy = card.AllocatedEnergy };
    }

    public void Record(GameAction action, IReadOnlyList<GameEvent> events, GameState after)
    {
        // New draws may not exist in the pre-action snapshot. Only collect their
        // metadata here; each event below decides whether a name is public.
        foreach (PlayerState player in after.Players)
        {
            AddNew(player.Hand); AddNew(player.MonsterZones); AddNew(player.SpellTrapZones); AddNew(player.Graveyard);
            if (player.FieldSpell != null && !cards.ContainsKey(player.FieldSpell.InstanceId)) CopyCard(player.FieldSpell);
        }
        sequence++;
        string heading = "#" + sequence + " | Turn " + turn + " / " + phase + " | P" + (action.PlayerIndex + 1);
        for (int viewer = 0; viewer < 2; viewer++)
        {
            lines[viewer].Add(heading + " — " + ActionName(action.Type));
            if (action.TargetInstanceIds.Count > 0)
            {
                var targets = new List<string>();
                foreach (long id in action.TargetInstanceIds) targets.Add(Name(id, viewer));
                lines[viewer].Add("  Selected target(s): " + string.Join(", ", targets));
            }
        }
        foreach (GameEvent entry in events) RecordEvent(entry);
        Capture(after);
        Revision++;
    }

    private void AddNew(IReadOnlyList<CardInstance> source)
    {
        foreach (CardInstance card in source)
            if (card != null && !cards.ContainsKey(card.InstanceId)) CopyCard(card);
    }

    private void RecordEvent(GameEvent e)
    {
        cards.TryGetValue(e.CardInstanceId, out Card card);
        int player = e.PlayerIndex;
        string who = "P" + (player + 1), change = null;
        switch (e.Type)
        {
            case GameEventType.MonsterDamaged:
                if (card != null) { int old = card.Hp; card.Hp = Math.Max(0, old - e.Amount); change = "HP " + old + " → " + card.Hp + " (damage " + e.Amount + ")"; }
                break;
            case GameEventType.MonsterHealthModified:
                if (card != null) { int old = card.Hp; card.Hp = Math.Max(0, Math.Min(card.MaxHp, old + e.Amount)); change = "HP " + old + " → " + card.Hp; }
                break;
            case GameEventType.MonsterMaximumHealthModified:
                if (card != null)
                {
                    int old = card.MaxHp; card.MaxHp = Math.Max(0, old + e.Amount);
                    change = "maximum HP " + old + " → " + card.MaxHp;
                }
                break;
            case GameEventType.MonsterAttackModified:
                if (card != null) change = "ATK modifier " + (e.Amount >= 0 ? "+" : "") + e.Amount;
                break;
            case GameEventType.MonsterDefenseModified:
                if (card != null) change = "DEF modifier " + (e.Amount >= 0 ? "+" : "") + e.Amount;
                break;
            case GameEventType.EnergyAllocated:
            case GameEventType.AllocatedEnergyRemoved:
                if (card != null)
                {
                    int old = card.Energy; card.Energy = Math.Max(0, old + (e.Type == GameEventType.EnergyAllocated ? e.Amount : -e.Amount));
                    change = "allocated Energy " + old + " → " + card.Energy;
                }
                break;
            case GameEventType.PlayerDamaged:
                int oldLp = lp[player]; lp[player] = Math.Max(0, oldLp - e.Amount);
                change = "LP " + oldLp + " → " + lp[player] + " (lost " + e.Amount + ")";
                break;
            case GameEventType.EnergyRefreshed:
            case GameEventType.EnergySpent:
                int oldEnergy = energy[player]; energy[player] = e.Type == GameEventType.EnergyRefreshed ? e.Amount : Math.Max(0, oldEnergy - e.Amount);
                change = "Energy " + oldEnergy + " → " + energy[player];
                break;
        }
        bool reveal = e.Type == GameEventType.MonsterSummoned || e.Type == GameEventType.SpellActivated ||
            e.Type == GameEventType.TrapActivated || e.Type == GameEventType.FieldSpellActivated ||
            e.Type == GameEventType.CardSentToGraveyard || e.Type == GameEventType.MonsterTributed ||
            e.Type == GameEventType.ChainLinkAdded || e.Type == GameEventType.EffectResolved;
        if (reveal && card != null) card.Hidden = false;
        if (e.Type == GameEventType.CardSet && card != null) card.Hidden = true;
        for (int viewer = 0; viewer < 2; viewer++)
        {
            string name = Name(e.CardInstanceId, viewer);
            string target = Name(e.TargetInstanceId, viewer);
            string message;
            if (change != null) message = (card != null ? name : who) + ": " + change;
            else switch (e.Type)
            {
                case GameEventType.CardDrawn:
                    message = who + " drew 1 card" + (player == viewer ? ": " + name : "."); break;
                case GameEventType.CardAddedToHand:
                    message = who + " added 1 card to hand" + (player == viewer ? ": " + name : "."); break;
                case GameEventType.TurnStarted: message = "TURN " + e.Amount + " — " + who; break;
                case GameEventType.PhaseChanged: message = who + " entered " + e.Phase + "."; break;
                case GameEventType.RecoveryStepChanged: message = who + ": Recovery / " + (RecoveryStep)e.Amount; break;
                case GameEventType.DrawStepSkipped: message = who + " skipped Draw."; break;
                case GameEventType.EnergyRefreshSkipped: message = who + " skipped Energy Refresh."; break;
                case GameEventType.StandbyPhaseSkipped: message = who + " skipped Standby."; break;
                case GameEventType.BattlePhaseSkipped: message = who + " skipped Battle."; break;
                case GameEventType.MonsterSummoned: message = who + " summoned " + name + " to Monster Zone " + (e.Amount + 1) + "."; break;
                case GameEventType.MonsterTributed: message = who + " tributed " + name + " (not KO)."; break;
                case GameEventType.BattlePositionChanged: message = name + " changed to " + (BattlePosition)e.Amount + "."; break;
                case GameEventType.AttackDeclared: message = name + (e.TargetInstanceId == 0 ? " declared a direct attack." : " attacked " + target + "."); break;
                case GameEventType.MonsterDestroyed: message = name + " was KO'd."; break;
                case GameEventType.CardPlacedInSpellTrapZone: message = who + " placed " + name + " in Spell/Trap Zone " + (e.Amount + 1) + "."; break;
                case GameEventType.CardSet: message = who + " set " + name + " in Spell/Trap Zone " + (e.Amount + 1) + "."; break;
                case GameEventType.SpellActivated: message = who + " activated Spell " + name + "."; break;
                case GameEventType.TrapActivated: message = who + " activated Trap " + name + "."; break;
                case GameEventType.FieldSpellActivated: message = who + " activated Field Spell " + name + "."; break;
                case GameEventType.EquipAttached: message = name + " equipped to " + target + "."; break;
                case GameEventType.ChainStarted: message = who + " opened a chain."; break;
                case GameEventType.ChainLinkAdded: message = "Chain Link " + e.Amount + ": " + who + " / " + name; break;
                case GameEventType.ResponsePassed: message = who + " passed response priority."; break;
                case GameEventType.CardActivationNegated: message = name + ": activation negated."; break;
                case GameEventType.EffectResolved: message = name + ": effect resolved."; break;
                case GameEventType.CardSentToGraveyard: message = name + " → Graveyard."; break;
                case GameEventType.DeckShuffled: message = who + " shuffled their Deck."; break;
                case GameEventType.TriggeredEffectOffered: message = who + ": triggered effect offered for " + name + "."; break;
                case GameEventType.TriggeredEffectDeclined: message = who + " declined the triggered effect of " + name + "."; break;
                case GameEventType.FieldMaintenancePaid: message = who + " paid " + e.Amount + " Energy for " + name + " maintenance."; break;
                case GameEventType.MatchEnded: message = player < 0 ? "DUEL ENDED — DRAW" : "DUEL ENDED — " + who + " wins."; break;
                default: message = who + ": " + e.Type + " (" + e.Amount + ")"; break;
            }
            lines[viewer].Add("  " + message);
        }
    }

    private string Name(long id, int viewer)
    {
        if (!cards.TryGetValue(id, out Card card)) return "card";
        return card.Hidden && card.Owner != viewer ? "a hidden card" : card.Name + " [" + id + "]";
    }
    private void Add(string message)
    {
        foreach (List<string> list in lines) list.Add(message);
        Revision++;
    }
    private static string ActionName(GameActionType type)
    {
        switch (type)
        {
            case GameActionType.SummonMonster: return "Summon Monster";
            case GameActionType.SetTrap: return "Set Trap";
            case GameActionType.ActivateCard:
            case GameActionType.ActivateNormalSpell: return "Activate card/effect";
            case GameActionType.Attack: return "Declare attack";
            case GameActionType.AllocateEnergy: return "Allocate Energy";
            case GameActionType.ChangeBattlePosition: return "Change battle position";
            case GameActionType.AdvancePhase: return "Advance phase";
            case GameActionType.EnterEndPhase: return "Enter End Phase";
            case GameActionType.EndTurn: return "End turn";
            case GameActionType.PassResponse: return "Pass response";
            case GameActionType.ResolveTriggeredEffect: return "Resolve triggered effect";
            case GameActionType.DeclineTriggeredEffect: return "Decline triggered effect";
            case GameActionType.ResolveDrawStep: return "Resolve Draw Step";
            case GameActionType.ResolveEnergyRefreshStep: return "Resolve Energy Refresh";
            default: return type.ToString();
        }
    }
}
