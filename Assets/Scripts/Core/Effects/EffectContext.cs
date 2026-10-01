using System;
using System.Collections.Generic;

[Serializable]
public sealed class EffectContext
{
    private static readonly long[] NoTargets = new long[0];

    public GameState State { get; }
    public int ActivatingPlayerIndex { get; }
    public long SourceInstanceId { get; }
    public IReadOnlyList<long> TargetInstanceIds { get; }

    public PlayerState ActivatingPlayer =>
        State.Players[ActivatingPlayerIndex];

    public PlayerState Opponent =>
        State.Players[1 - ActivatingPlayerIndex];

    public EffectContext(
        GameState state,
        int activatingPlayerIndex,
        long sourceInstanceId,
        IReadOnlyList<long> targetInstanceIds = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));

        if (activatingPlayerIndex < 0 ||
            activatingPlayerIndex >= state.Players.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activatingPlayerIndex));
        }

        ActivatingPlayerIndex = activatingPlayerIndex;
        SourceInstanceId = sourceInstanceId;
        TargetInstanceIds = CopyTargets(targetInstanceIds);
    }

    private static IReadOnlyList<long> CopyTargets(
        IReadOnlyList<long> targets)
    {
        if (targets == null || targets.Count == 0)
        {
            return NoTargets;
        }

        long[] copy = new long[targets.Count];

        for (int index = 0; index < targets.Count; index++)
        {
            copy[index] = targets[index];
        }

        return copy;
    }
}
