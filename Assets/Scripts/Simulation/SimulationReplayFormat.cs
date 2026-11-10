using System;
using System.Collections.Generic;
using System.Linq;

public static class SimulationReplayFormat
{
    public const int TraceSchema = 2;
    public static string RulesHash() => SimulationJson.Hash(SimulationJson.Encode(GameConfig.CreateDefault()));
    public static string DefinitionsHash(IEnumerable<CardDefinition> cards)
    {
        var unique = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (CardDefinition card in cards)
        {
            string encoded = SimulationJson.Encode(card);
            if (unique.TryGetValue(card.CardId, out string previous) && previous != encoded)
                throw new InvalidOperationException("Conflicting card definitions: " + card.CardId);
            unique[card.CardId] = encoded;
        }
        return SimulationJson.Hash(SimulationJson.Encode(unique));
    }
}
