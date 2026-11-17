using System;
using System.Collections.Generic;

public enum ResponseWindowType
{
    CardActivation,
    AttackDeclaration
}

[Serializable]
public sealed class PendingAttack
{
    public int PlayerIndex { get; }
    public long AttackerInstanceId { get; }
    public long TargetInstanceId { get; }

    public PendingAttack(
        int playerIndex,
        long attackerInstanceId,
        long targetInstanceId)
    {
        PlayerIndex = playerIndex;
        AttackerInstanceId = attackerInstanceId;
        TargetInstanceId = targetInstanceId;
    }
}

[Serializable]
public sealed class ChainLink
{
    private readonly long[] targetInstanceIds;

    public int PlayerIndex { get; }
    public long SourceInstanceId { get; }
    public int SourceZoneIndex { get; }
    public CardSpeed Speed { get; }
    public EffectDefinition Effect { get; }
    public IReadOnlyList<long> TargetInstanceIds => targetInstanceIds;
    public bool IsNegated { get; private set; }

    public ChainLink(
        int playerIndex,
        long sourceInstanceId,
        int sourceZoneIndex,
        CardSpeed speed,
        EffectDefinition effect,
        IReadOnlyList<long> targets)
    {
        PlayerIndex = playerIndex;
        SourceInstanceId = sourceInstanceId;
        SourceZoneIndex = sourceZoneIndex;
        Speed = speed;
        Effect = effect ?? throw new ArgumentNullException(nameof(effect));
        targetInstanceIds = targets == null
            ? new long[0]
            : CopyTargets(targets);
    }

    public void Negate()
    {
        IsNegated = true;
    }

    private static long[] CopyTargets(IReadOnlyList<long> targets)
    {
        long[] copy = new long[targets.Count];
        for (int index = 0; index < targets.Count; index++)
            copy[index] = targets[index];
        return copy;
    }
}

[Serializable]
public sealed class ChainState
{
    private readonly List<ChainLink> links = new List<ChainLink>();

    public ResponseWindowType WindowType { get; }
    public IReadOnlyList<ChainLink> Links => links;
    public PendingAttack PendingAttack { get; }
    public int PriorityPlayerIndex { get; private set; }
    public int ConsecutivePasses { get; private set; }
    public int ResolvingLinkIndex { get; private set; } = -1;

    public ChainState(
        ResponseWindowType windowType,
        int priorityPlayerIndex,
        PendingAttack pendingAttack = null)
    {
        WindowType = windowType;
        PriorityPlayerIndex = priorityPlayerIndex;
        PendingAttack = pendingAttack;
    }

    internal ChainState CopyForSearch(SearchCloneContext context)
    {
        var attack = PendingAttack == null ? null : new PendingAttack(PendingAttack.PlayerIndex,
            PendingAttack.AttackerInstanceId, PendingAttack.TargetInstanceId);
        var copy = new ChainState(WindowType, PriorityPlayerIndex, attack) {
            ConsecutivePasses = ConsecutivePasses, ResolvingLinkIndex = ResolvingLinkIndex };
        foreach (ChainLink link in links) copy.links.Add(context.Link(link));
        return copy;
    }

    public void AddLink(ChainLink link)
    {
        links.Add(link ?? throw new ArgumentNullException(nameof(link)));
        ConsecutivePasses = 0;
        PriorityPlayerIndex = 1 - link.PlayerIndex;
    }

    public void Pass()
    {
        ConsecutivePasses++;
        PriorityPlayerIndex = 1 - PriorityPlayerIndex;
    }

    public void BeginResolvingLink(int index)
    {
        ResolvingLinkIndex = index;
    }

    public bool TryNegatePreviousLink(out ChainLink negatedLink)
    {
        int previousIndex = ResolvingLinkIndex - 1;
        if (previousIndex < 0 || previousIndex >= links.Count)
        {
            negatedLink = null;
            return false;
        }

        negatedLink = links[previousIndex];
        negatedLink.Negate();
        return true;
    }
}
