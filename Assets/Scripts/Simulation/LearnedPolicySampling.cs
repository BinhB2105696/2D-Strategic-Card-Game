using System;
using System.Collections.Generic;

internal static class LearnedPolicySampling
{
    internal const double UniformMix = 0.05;
    internal const double LowUniformMix = 0.02;
    internal const double LowTemperature = 0.35;

    internal static int Select(IReadOnlyList<float> logits, double draw, out double logProbability,
        double temperature = 1, double uniformMix = UniformMix)
    {
        double max = logits[LearnedPolicy.SelectedIndex(logits)];
        var masses = new double[logits.Count];
        double sum = 0;
        for (int i = 0; i < masses.Length; i++)
        {
            masses[i] = Math.Exp((logits[i] - max) / temperature);
            sum += masses[i];
        }
        for (int i = 0; i < masses.Length; i++)
        {
            double probability = (1 - uniformMix) * masses[i] / sum + uniformMix / masses.Length;
            if (draw < probability || i == masses.Length - 1)
            {
                logProbability = Math.Log(probability);
                return i;
            }
            draw -= probability;
        }
        throw new InvalidOperationException("Expected at least one legal action to sample.");
    }
}
