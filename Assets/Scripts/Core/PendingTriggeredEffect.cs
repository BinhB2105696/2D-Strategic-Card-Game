using System;
using System.Collections.Generic;

[Serializable]
public sealed class PendingTriggeredEffect
{
    private readonly long[] candidateInstanceIds;

    public int PlayerIndex { get; }
    public long SourceInstanceId { get; }
    public EffectDefinition Effect { get; }
    public IReadOnlyList<long> CandidateInstanceIds => candidateInstanceIds;

    public PendingTriggeredEffect(
        int playerIndex,
        long sourceInstanceId,
        EffectDefinition effect,
        IReadOnlyList<long> candidates)
    {
        if (effect == null)
            throw new ArgumentNullException(nameof(effect));
        if (candidates == null || candidates.Count == 0)
            throw new ArgumentException(
                "A pending triggered effect requires at least one candidate.",
                nameof(candidates));

        PlayerIndex = playerIndex;
        SourceInstanceId = sourceInstanceId;
        Effect = effect;
        candidateInstanceIds = new long[candidates.Count];
        for (int index = 0; index < candidates.Count; index++)
            candidateInstanceIds[index] = candidates[index];
    }

    public bool ContainsCandidate(long instanceId)
    {
        for (int index = 0; index < candidateInstanceIds.Length; index++)
        {
            if (candidateInstanceIds[index] == instanceId)
                return true;
        }
        return false;
    }
}
