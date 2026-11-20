using System;
using System.Collections.Generic;
using UnityEngine;

public enum DuelBotDifficulty { Easy, Normal, Hard }

public sealed class AppMatchRequest
{
    public string matchId;
    public string startedUtc;
    public uint seed;
    public LocalPlayerProfile profile;
    public SavedDeck deck;
    public Dictionary<string, CardData> catalog;
    public CardData[] playerCards;
    public Sprite avatar;
    public GameObject overlayPrefab;
    public DuelBotDifficulty difficulty;
    public string OpponentName => difficulty + " Bot";
}

// One-use scene handoff; direct Play of SampleScene keeps its original demo.
public static class AppMatchLaunch
{
    private static AppMatchRequest pending;
    public static bool ReturningHome { get; set; }

    public static void Prepare(LocalPlayerProfile profile, SavedDeck deck,
        IReadOnlyDictionary<string, CardData> catalog, Sprite avatar, GameObject overlayPrefab,
        DuelBotDifficulty difficulty = DuelBotDifficulty.Easy)
    {
        var cards = new List<CardData>();
        foreach (string id in deck.cardIds) cards.Add(catalog[id]);
        var catalogCopy = new Dictionary<string, CardData>();
        foreach (var item in catalog) catalogCopy.Add(item.Key, item.Value);
        pending = new AppMatchRequest
        {
            matchId = Guid.NewGuid().ToString("N"), startedUtc = DateTime.UtcNow.ToString("o"),
            seed = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0),
            profile = profile,
            deck = new SavedDeck { deckId = deck.deckId, name = deck.name, cardIds = new List<string>(deck.cardIds) },
            catalog = catalogCopy, playerCards = cards.ToArray(), avatar = avatar, overlayPrefab = overlayPrefab,
            difficulty = difficulty
        };
    }

    public static AppMatchRequest Consume()
    {
        AppMatchRequest result = pending;
        pending = null;
        return result;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { pending = null; ReturningHome = false; }
}
