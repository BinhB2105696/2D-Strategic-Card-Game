using System;
using System.Collections.Generic;

[Serializable]
public sealed class MatchHeader
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentRulesVersion = 6;
    public const int CurrentCardCatalogVersion = 4;

    public int SchemaVersion { get; }
    public int RulesVersion { get; }
    public int CardCatalogVersion { get; }
    public int RandomAlgorithmVersion { get; }
    public uint Seed { get; }

    public MatchHeader(uint seed)
    {
        SchemaVersion = CurrentSchemaVersion;
        RulesVersion = CurrentRulesVersion;
        CardCatalogVersion = CurrentCardCatalogVersion;
        RandomAlgorithmVersion = SeededGameRandom.AlgorithmVersion;
        Seed = seed;
    }
}

[Serializable]
public sealed class ActionRecord
{
    public int SequenceNumber { get; }
    public GameAction Action { get; }

    public ActionRecord(int sequenceNumber, GameAction action)
    {
        SequenceNumber = sequenceNumber;
        Action = action;
    }
}

[Serializable]
public sealed class MatchLog
{
    private readonly List<ActionRecord> actions =
        new List<ActionRecord>();

    public MatchHeader Header { get; }
    public IReadOnlyList<ActionRecord> Actions => actions;

    public MatchLog(uint seed)
    {
        Header = new MatchHeader(seed);
    }

    internal void Add(GameAction action)
    {
        actions.Add(new ActionRecord(actions.Count + 1, action));
    }
}
