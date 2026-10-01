using System;

[Serializable]
public sealed class GameConfig
{
    public int StartingPlayerHealth { get; }
    public int MinimumDeckSize { get; }
    public int MaximumDeckSize { get; }
    public int StartingHandSize { get; }
    public int CardsDrawnPerTurn { get; }
    public int BaseMaximumEnergy { get; }
    public int NormalSummonEnergyCost { get; }
    public int MonsterZoneCount { get; }
    public int SpellTrapZoneCount { get; }
    public int DestructionPenaltyPerEnergy { get; }

    public GameConfig(
        int startingPlayerHealth,
        int minimumDeckSize,
        int maximumDeckSize,
        int startingHandSize,
        int cardsDrawnPerTurn,
        int baseMaximumEnergy,
        int normalSummonEnergyCost,
        int monsterZoneCount,
        int spellTrapZoneCount,
        int destructionPenaltyPerEnergy)
    {
        if (startingPlayerHealth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startingPlayerHealth));
        }

        if (minimumDeckSize <= 0 || maximumDeckSize < minimumDeckSize)
        {
            throw new ArgumentException("Deck size range is invalid.");
        }

        if (startingHandSize < 0 || startingHandSize > minimumDeckSize)
        {
            throw new ArgumentOutOfRangeException(nameof(startingHandSize));
        }

        if (cardsDrawnPerTurn < 0 || baseMaximumEnergy < 0 ||
            normalSummonEnergyCost < 0 || monsterZoneCount <= 0 ||
            spellTrapZoneCount <= 0 || destructionPenaltyPerEnergy < 0)
        {
            throw new ArgumentException(
                "Game configuration values cannot be negative, and zone " +
                "counts must be greater than zero.");
        }

        StartingPlayerHealth = startingPlayerHealth;
        MinimumDeckSize = minimumDeckSize;
        MaximumDeckSize = maximumDeckSize;
        StartingHandSize = startingHandSize;
        CardsDrawnPerTurn = cardsDrawnPerTurn;
        BaseMaximumEnergy = baseMaximumEnergy;
        NormalSummonEnergyCost = normalSummonEnergyCost;
        MonsterZoneCount = monsterZoneCount;
        SpellTrapZoneCount = spellTrapZoneCount;
        DestructionPenaltyPerEnergy = destructionPenaltyPerEnergy;
    }

    public static GameConfig CreateDefault()
    {
        return new GameConfig(
            startingPlayerHealth: 4000,
            minimumDeckSize: 30,
            maximumDeckSize: 40,
            startingHandSize: 4,
            cardsDrawnPerTurn: 1,
            baseMaximumEnergy: 5,
            normalSummonEnergyCost: 1,
            monsterZoneCount: 3,
            spellTrapZoneCount: 3,
            destructionPenaltyPerEnergy: 100);
    }
}
