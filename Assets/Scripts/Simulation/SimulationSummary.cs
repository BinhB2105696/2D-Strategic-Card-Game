using System;
using System.Collections.Generic;
using System.Linq;

public static class SimulationSummary
{
    public static object Create(List<SimulationMatchResult> results, SimulationConfig config, bool includeIntervals = true)
    {
        var groups = new List<object>();
        foreach (var group in results.GroupBy(result => result.scenario))
        {
            SimulationMatchup matchup = config.matchups[group.Key];
            var finished = group.Where(result => result.status == "Finished").ToArray();
            string reference = matchup.player0Bot;
            var pairs = group.GroupBy(result => result.pairId)
                .Where(pair => pair.Count() == (matchup.swapBots ? 2 : 1) && pair.All(result => result.status == "Finished"))
                .ToArray();
            double[] scores = matchup.player0Bot == matchup.player1Bot ? new double[0] : pairs.Select(pair => pair.Average(result => {
                int side = result.bot0 == reference ? 0 : 1;
                return result.winner < 0 ? 0.5 : result.winner == side ? 1.0 : 0.0;
            })).ToArray();
            object interval = null;
            if (includeIntervals && scores.Length >= 2)
            {
                var random = new Random(1229); var bootstrap = new double[1000];
                for (int sample = 0; sample < bootstrap.Length; sample++)
                {
                    double sum = 0;
                    for (int i = 0; i < scores.Length; i++) sum += scores[random.Next(scores.Length)];
                    bootstrap[sample] = sum / scores.Length;
                }
                Array.Sort(bootstrap);
                interval = new[] { bootstrap[24], bootstrap[974] };
            }
            groups.Add(SimulationJson.Object("scenario", group.Key, "matchup", matchup, "attempted", group.Count(),
                "finished", finished.Length, "draws", finished.Count(r => r.winner < 0),
                "slot0Wins", finished.Count(r => r.winner == 0), "slot1Wins", finished.Count(r => r.winner == 1),
                "firstPlayerWins", finished.Count(r => r.winner >= 0 && r.winner == r.firstPlayer),
                "truncated", group.Count(r => SimulationEngine.IsTruncated(r.status)),
                "errors", group.Count(r => r.status != "Finished" && !SimulationEngine.IsTruncated(r.status)),
                "referenceBot", reference, "completeSeedPairs", pairs.Length,
                "pairedScoreRate", scores.Length == 0 ? null : (object)scores.Average(),
                "pairedScore95Interval", interval));
        }
        return SimulationJson.Object("schema", 1, "matches", results.Count, "groups", groups,
            "intervalMethod", includeIntervals
                ? "Percentile bootstrap, 1000 resamples over complete seed pairs; reporting RNG seed 1229. Same-bot score comparisons omitted."
                : "Intervals deferred until batch completion. Same-bot score comparisons omitted.",
            "caution", "Pilot runs are diagnostics, not evidence of stronger play. Errors/truncations are not draws or losses. Review completion rates and deck/first-player strata.");
    }
}
