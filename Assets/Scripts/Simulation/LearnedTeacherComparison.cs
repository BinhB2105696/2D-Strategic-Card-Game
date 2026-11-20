using System.Collections.Generic;

internal sealed class LearnedTeacherComparison
{
    private readonly NormalBot normal = new NormalBot();
    private readonly HardBot hard;

    internal LearnedTeacherComparison(IReadOnlyList<CardDefinition> publicCatalog, IReadOnlyList<CardDefinition> ownDeck)
    {
        hard = new HardBot(publicCatalog, knownOwnStartingDeck: ownDeck);
    }

    internal object Compare(GameState state, BotGameStateView view, IReadOnlyList<GameAction> legal)
    {
        if (legal.Count < 2 || state.TurnNumber > 8 || state.InteractionPlayerIndex != state.CurrentPlayerIndex ||
            state.HasOpenResponseWindow || state.HasPendingTriggeredEffect ||
            (state.CurrentPhase != TurnPhase.Main && state.CurrentPhase != TurnPhase.Battle))
            return null;

        int player = state.InteractionPlayerIndex;
        normal.Observe(state, player);
        int normalIndex = SimulationAiContract.SelectedIndex(legal, normal.ChooseAction(view, legal));
        hard.Observe(state, player);
        int hardIndex = SimulationAiContract.SelectedIndex(legal, hard.ChooseAction(view, legal));
        return SimulationJson.Object("normalIndex", normalIndex, "hardIndex", hardIndex,
            "hardIterations", hard.LastIterations, "hardFallback", hard.LastFallbackReason);
    }
}
