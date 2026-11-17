using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class GameState
{
    // An exact, isolated copy for verification/branching. Never expose its hidden data to a policy.
    public GameState CreateSearchFork() => new GameState(this, null);

    // Search chance events use a separate seed, never the actual match RNG stream.
    public GameState CreateSearchFork(uint simulationSeed) => new GameState(this, simulationSeed);

    // Prototype information-set model: quiet decision states, public catalog prior, no true hidden identities.
    public GameState CreateInformationSetFork(int observer, IReadOnlyList<CardDefinition> publicCatalog, uint simulationSeed,
        IReadOnlyList<CardDefinition> knownOwnStartingDeck = null)
    {
        if (observer < 0 || observer > 1) throw new ArgumentOutOfRangeException(nameof(observer));
        if (HasOpenResponseWindow || HasPendingTriggeredEffect || queuedTriggeredEffects.Count != 0)
            throw new InvalidOperationException("Information-set search currently starts only at quiet decision states.");
        if (publicCatalog == null || publicCatalog.Count == 0) throw new ArgumentException("An explicit public card catalog is required.", nameof(publicCatalog));
        CardDefinition[] catalog = publicCatalog.OrderBy(card => card.CardId, StringComparer.Ordinal).ToArray();
        var definitions = catalog.ToDictionary(card => card.CardId, StringComparer.Ordinal);
        var known = new Dictionary<long, CardDefinition>();
        Action<CardInstance> reveal = card => {
            if (card == null) return;
            if (!definitions.TryGetValue(card.Definition.CardId, out CardDefinition definition))
                throw new InvalidOperationException("Public card is missing from the search catalog.");
            known[card.InstanceId] = definition;
        };
        foreach (PlayerState owner in players)
        {
            if (owner.PlayerIndex == observer) foreach (CardInstance card in owner.Hand) reveal(card);
            foreach (CardInstance card in owner.MonsterZones)
                if (card != null && (owner.PlayerIndex == observer || !card.IsFaceDown)) reveal(card);
            foreach (CardInstance card in owner.SpellTrapZones)
                if (card != null && (owner.PlayerIndex == observer || !card.IsFaceDown)) reveal(card);
            if (owner.FieldSpell != null && (owner.PlayerIndex == observer || !owner.FieldSpell.IsFaceDown)) reveal(owner.FieldSpell);
            foreach (CardInstance card in owner.Graveyard) reveal(card);
        }
        foreach (BotCardView choice in BotGameStateView.Create(this, observer).ChoiceCards)
            if (choice.IsKnown && definitions.TryGetValue(choice.CardId, out CardDefinition definition)) known[choice.InstanceId] = definition;
        foreach (StatModifierApplication modifier in statModifiers)
            if (!known.ContainsKey(modifier.Target.InstanceId) || !known.ContainsKey(modifier.SourceInstanceId))
                throw new InvalidOperationException("A modifier references an unobserved card; this search model cannot safely represent it.");
        Dictionary<string, int> ownRemaining = null;
        if (knownOwnStartingDeck != null)
        {
            ownRemaining = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (CardDefinition card in knownOwnStartingDeck)
            {
                if (!definitions.ContainsKey(card.CardId)) throw new InvalidOperationException("Own starting deck is missing from the public catalog.");
                ownRemaining.TryGetValue(card.CardId, out int count); ownRemaining[card.CardId] = count + 1;
            }
            PlayerState own = players[observer];
            // Only initial owned deck knowledge and currently observed owned cards; never read hidden deck identities.
            foreach (CardInstance card in own.Hand.Concat(own.Graveyard).Concat(own.MonsterZones)
                .Concat(own.SpellTrapZones).Concat(new[] { own.FieldSpell }))
            {
                if (card == null) continue;
                if (!ownRemaining.TryGetValue(card.Definition.CardId, out int count) || count < 1)
                    throw new InvalidOperationException("Observed owned cards cannot be reconciled with the initial deck; a richer belief model is required.");
                ownRemaining[card.Definition.CardId] = count - 1;
            }
        }
        var fork = CreateSearchFork(simulationSeed);
        long syntheticId = long.MaxValue - 1;
        foreach (PlayerState owner in fork.players) owner.SampleHiddenForSearch(observer, known, catalog, fork.Random,
            ref syntheticId, owner.PlayerIndex == observer ? ownRemaining : null);
        return fork;
    }

    private GameState(GameState source, uint? simulationSeed)
    {
        var context = new SearchCloneContext();
        Config = source.Config;
        Random = simulationSeed.HasValue ? new SeededGameRandom(simulationSeed.Value) : source.Random.CopyForSearch();
        players = new PlayerState[source.players.Length];
        for (int i = 0; i < players.Length; i++) players[i] = source.players[i].CopyForSearch(context);
        CurrentPlayerIndex = source.CurrentPlayerIndex;
        TurnNumber = source.TurnNumber;
        CurrentPhase = source.CurrentPhase;
        CurrentRecoveryStep = source.CurrentRecoveryStep;
        NextInstanceId = source.NextInstanceId;
        IsFinished = source.IsFinished;
        WinnerPlayerIndex = source.WinnerPlayerIndex;
        ActiveChain = source.ActiveChain?.CopyForSearch(context);
        PendingTriggeredEffect = context.Pending(source.PendingTriggeredEffect);
        foreach (PendingTriggeredEffect pending in source.queuedTriggeredEffects) queuedTriggeredEffects.Add(context.Pending(pending));
        foreach (var pair in source.effectUses) effectUses.Add(pair.Key, pair.Value);
        foreach (StatModifierApplication modifier in source.statModifiers)
            statModifiers.Add(new StatModifierApplication(modifier.SourceInstanceId, context.Card(modifier.Target),
                modifier.AttackAmount, modifier.DefenseAmount, modifier.ExpireAfterTurn, modifier.MaximumHealthAmount, modifier.IsAura));
        Array.Copy(source.mainStartMonsterCounts, mainStartMonsterCounts, mainStartMonsterCounts.Length);
        Array.Copy(source.mainStartHealth, mainStartHealth, mainStartHealth.Length);
        foreach (RetaliationMark mark in source.retaliationMarks)
            retaliationMarks.Add(new RetaliationMark { Source = mark.Source, Opponent = mark.Opponent,
                Reduction = mark.Reduction, ExpiresAfterTurn = mark.ExpiresAfterTurn });
    }
}

// Preserve card/pending/link aliasing without reflection or writes into source objects.
internal sealed class SearchCloneContext
{
    private readonly Dictionary<CardInstance, CardInstance> cards = new Dictionary<CardInstance, CardInstance>();
    private readonly Dictionary<PendingTriggeredEffect, PendingTriggeredEffect> pending = new Dictionary<PendingTriggeredEffect, PendingTriggeredEffect>();
    private readonly Dictionary<ChainLink, ChainLink> links = new Dictionary<ChainLink, ChainLink>();

    internal CardInstance Card(CardInstance source)
    {
        if (source == null) return null;
        if (!cards.TryGetValue(source, out CardInstance copy)) { copy = source.CopyForSearch(); cards.Add(source, copy); }
        return copy;
    }

    internal PendingTriggeredEffect Pending(PendingTriggeredEffect source)
    {
        if (source == null) return null;
        if (!pending.TryGetValue(source, out PendingTriggeredEffect copy))
        {
            copy = new PendingTriggeredEffect(source.PlayerIndex, source.SourceInstanceId, source.Effect, source.CandidateInstanceIds);
            pending.Add(source, copy);
        }
        return copy;
    }

    internal ChainLink Link(ChainLink source)
    {
        if (!links.TryGetValue(source, out ChainLink copy))
        {
            copy = new ChainLink(source.PlayerIndex, source.SourceInstanceId, source.SourceZoneIndex,
                source.Speed, source.Effect, source.TargetInstanceIds);
            if (source.IsNegated) copy.Negate();
            links.Add(source, copy);
        }
        return copy;
    }
}
