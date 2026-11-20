using System;
using System.Collections.Generic;

internal sealed class LearnedBot : IBot, IStateObservingBot
{
    private readonly LearnedPolicy policy;
    private readonly SeededGameRandom exploration;
    private readonly double temperature, uniformMix;
    private GameState observed;
    private int observer;

    public string DisplayName => exploration == null ? "Learned" : "LearnedTrain";
    internal double? LastBehaviorLogProbability { get; private set; }
    internal object Describe() => SimulationJson.Object("bot", DisplayName, "policySha256", policy.Sha256,
        "uniformExploration", exploration == null ? (double?)null : uniformMix,
        "explorationTemperature", exploration == null ? (double?)null : temperature,
        "explorationSeed", exploration?.InitialSeed);

    internal LearnedBot(LearnedPolicy policy, SeededGameRandom exploration = null, bool lowerExploration = false)
    {
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.exploration = exploration;
        temperature = lowerExploration ? LearnedPolicySampling.LowTemperature : 1;
        uniformMix = lowerExploration ? LearnedPolicySampling.LowUniformMix : LearnedPolicySampling.UniformMix;
    }

    public void Observe(GameState state, int playerIndex)
    {
        observed = state;
        observer = playerIndex;
    }

    public GameAction ChooseAction(BotGameStateView view, IReadOnlyList<GameAction> legalActions)
    {
        GameState state = observed;
        observed = null;
        LastBehaviorLogProbability = null;
        if (state == null || observer != view.ViewingPlayerIndex || observer != state.InteractionPlayerIndex)
            throw new InvalidOperationException("Learned requires a current observation for the acting player.");
        if (legalActions.Count == 1) return legalActions[0];
        var input = SimulationAiContract.Capture(state, observer, legalActions);
        float[] scores = policy.Score(input);
        if (exploration == null) return legalActions[LearnedPolicy.SelectedIndex(scores)];
        int selected = LearnedPolicySampling.Select(scores, exploration.NextUInt() / 4294967296.0, out double logProbability,
            temperature, uniformMix);
        LastBehaviorLogProbability = logProbability;
        return legalActions[selected];
    }
}
