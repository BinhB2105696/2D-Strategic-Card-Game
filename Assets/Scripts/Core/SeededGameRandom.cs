using System;
using System.Collections.Generic;

[Serializable]
public sealed class SeededGameRandom
{
    public const int AlgorithmVersion = 1;
    private const uint NonZeroFallbackSeed = 0x6D2B79F5u;

    private uint state;

    public uint InitialSeed { get; }
    public ulong GeneratedValueCount { get; private set; }

    public SeededGameRandom(uint seed)
    {
        InitialSeed = seed == 0 ? NonZeroFallbackSeed : seed;
        state = InitialSeed;
    }

    internal SeededGameRandom CopyForSearch() => (SeededGameRandom)MemberwiseClone();

    public uint NextUInt()
    {
        uint value = state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        state = value;
        GeneratedValueCount++;
        return value;
    }

    public int NextInt(int maximumExclusive)
    {
        if (maximumExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumExclusive));
        }

        uint range = (uint)maximumExclusive;
        uint rejectionThreshold = unchecked(0u - range) % range;
        uint value;

        do
        {
            value = NextUInt();
        }
        while (value < rejectionThreshold);

        return (int)(value % range);
    }

    public void Shuffle<T>(IList<T> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        for (int index = items.Count - 1; index > 0; index--)
        {
            int swapIndex = NextInt(index + 1);
            T temporary = items[index];
            items[index] = items[swapIndex];
            items[swapIndex] = temporary;
        }
    }
}
