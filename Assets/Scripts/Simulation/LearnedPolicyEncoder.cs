using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class LearnedPolicyEncoder
{
    internal const int Version = 1;
    internal const int FundamentalsVersion = 2;
    private const int EntityFeatures = 18;
    private const int BoardContextSize = 54;
    private const int FundamentalsActionSize = 4;
    private readonly bool fundamentals;
    private readonly int schema, globalCount, entityWidth;
    private readonly int[] zones, actionTypes;
    private readonly double[] missingEntity;
    private readonly Dictionary<string, int> vocabulary;

    internal int ContextSize => globalCount + 2 * zones.Length * (1 + entityWidth) + 1 + 2 * entityWidth + 2
        + (fundamentals ? BoardContextSize : 0);
    internal int ActionSize => 4 + actionTypes.Length + 3 * entityWidth + 4 + (fundamentals ? FundamentalsActionSize : 0);

    internal LearnedPolicyEncoder(int schema, int globalCount, string[] cardIds, int[] zones, int[] actionTypes, int version)
    {
        this.schema = schema;
        this.globalCount = globalCount;
        this.zones = zones;
        this.actionTypes = actionTypes;
        fundamentals = version == FundamentalsVersion;
        vocabulary = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < cardIds.Length; i++) vocabulary.Add(cardIds[i], i + 1);
        entityWidth = EntityFeatures + vocabulary.Count + 1;
        missingEntity = new double[entityWidth];
    }

    internal void Encode(Dictionary<string, object> policy, out double[] context, out double[][] actions)
    {
        var globals = (double[])policy["globalFeatures"];
        if ((int)policy["schema"] != schema || globals.Length != globalCount)
            throw new ArgumentException("Unexpected learned policy input schema.");

        var entities = ((IEnumerable<object>)policy["entities"]).Cast<Dictionary<string, object>>().Select(Entity).ToArray();
        double[] EntityAt(int index) => index == -1 ? missingEntity : entities[index];
        var marks = ((IEnumerable<object>)policy["retaliationMarks"]).Cast<Dictionary<string, object>>().ToArray();
        var candidates = ((IEnumerable<object>)policy["legalActions"]).Cast<Dictionary<string, object>>().ToArray();
        var mask = (int[])policy["legalActionMask"];
        if (mask.Length != candidates.Length || mask.Any(value => value != 1))
            throw new ArgumentException("Expected only filtered legal actions.");

        context = new double[ContextSize];
        Array.Copy(globals, context, globalCount);
        int offset = globalCount;
        for (int owner = 0; owner < 2; owner++)
        {
            foreach (int zone in zones)
            {
                var group = entities.Where(entity => entity[0] == owner && entity[1] == zone).ToArray();
                context[offset++] = group.Length / 10.0;
                MeanInto(group, context, offset, entityWidth);
                offset += entityWidth;
            }
        }
        context[offset++] = marks.Length / 10.0;
        MeanInto(marks.Select(mark => EntityAt((int)mark["source"]).Concat(EntityAt((int)mark["target"]))
            .Concat((double[])mark["features"]).ToArray()).ToArray(), context, offset, 2 * entityWidth + 2);
        int ownBoardCount = 0, ownReadyCount = 0;
        if (fundamentals)
        {
            foreach (double[] entity in entities.Where(entity => entity[1] == 2))
            {
                if ((entity[0] != 0 && entity[0] != 1) ||
                    (entity[2] != 0 && entity[2] != 1 && entity[2] != 2))
                    throw new ArgumentException("Unexpected board slot in rules-v6 policy input.");
                int slot = ContextSize - BoardContextSize + ((int)entity[0] * 3 + (int)entity[2]) * 9;
                var boardFeatures = new[] { 1.0, entity[3], entity[7], entity[9], entity[10],
                    EnergyShortfall(entity) / 5.0, entity[6], entity[15], entity[5] };
                Array.Copy(boardFeatures, 0, context, slot, boardFeatures.Length);
                if (entity[0] != 0) continue;
                ownBoardCount++;
                if (CanPrepareAttack(entity) && EnergyShortfall(entity) == 0) ownReadyCount++;
            }
        }

        actions = new double[candidates.Length][];
        for (int i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            var features = (double[])candidate["features"];
            int typeIndex = Array.IndexOf(actionTypes, (int)features[0]);
            if (typeIndex < 0) throw new ArgumentException("Unknown learned policy action type.");
            int source = (int)candidate["source"];
            var targets = (int[])candidate["targets"];
            var tributes = (int[])candidate["tributes"];
            var vector = new double[ActionSize];
            Array.Copy(features, vector, 4);
            vector[4 + typeIndex] = 1;
            offset = 4 + actionTypes.Length;
            Array.Copy(EntityAt(source), 0, vector, offset, entityWidth);
            MeanInto(targets.Select(EntityAt).ToArray(), vector, offset + entityWidth, entityWidth);
            MeanInto(tributes.Select(EntityAt).ToArray(), vector, offset + 2 * entityWidth, entityWidth);
            offset += 3 * entityWidth;
            AddRelations(marks, source, targets, "source", "target", globals[0], vector, offset);
            AddRelations(marks, source, targets, "target", "source", globals[0], vector, offset + 2);
            if (fundamentals)
            {
                int extra = ActionSize - FundamentalsActionSize;
                if (features[0] == 5)
                {
                    var monster = EntityAt(source);
                    int shortfall = EnergyShortfall(monster);
                    int remaining = Math.Max(0, shortfall - (int)Math.Round(5 * features[2]));
                    bool closesGap = shortfall > 0 && remaining == 0;
                    vector[extra] = remaining / 5.0;
                    vector[extra + 1] = closesGap ? 1 : 0;
                    vector[extra + 3] = (ownReadyCount + (closesGap && CanPrepareAttack(monster) ? 1 : 0)) / 3.0;
                }
                if (features[0] == 4) vector[extra + 2] = (ownBoardCount + 1 - tributes.Length) / 3.0;
            }
            actions[i] = vector;
        }
    }

    private static int EnergyShortfall(double[] entity) => Math.Max(0,
        (int)Math.Round(5 * entity[12]) - (int)Math.Round(5 * entity[11]));

    private static bool CanPrepareAttack(double[] entity) =>
        entity[1] == 2 && entity[3] == 1 && entity[6] == 1 && entity[15] > 0;

    private double[] Entity(Dictionary<string, object> entity)
    {
        var vector = new double[entityWidth];
        Array.Copy((double[])entity["features"], vector, EntityFeatures);
        vocabulary.TryGetValue((string)entity["cardId"], out int identity);
        vector[EntityFeatures + identity] = 1;
        return vector;
    }

    private static void MeanInto(double[][] vectors, double[] output, int offset, int width)
    {
        if (vectors.Length == 0) return;
        for (int i = 0; i < width; i++)
        {
            double sum = 0;
            foreach (double[] vector in vectors) sum += vector[i];
            output[offset + i] = sum / vectors.Length;
        }
    }

    private static void AddRelations(Dictionary<string, object>[] marks, int source, int[] targets,
        string from, string to, double turn, double[] vector, int offset)
    {
        double remaining = double.NegativeInfinity;
        foreach (var mark in marks)
        {
            if ((int)mark[from] != source || Array.IndexOf(targets, (int)mark[to]) < 0) continue;
            var features = (double[])mark["features"];
            vector[offset] += features[0];
            remaining = Math.Max(remaining, features[1] - turn);
        }
        vector[offset + 1] = double.IsNegativeInfinity(remaining) ? 0 : remaining;
    }
}
