using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class SavedDeck
{
    public string deckId;
    public string name;
    public List<string> cardIds = new List<string>();
}

[Serializable]
public sealed class LocalPlayerProfile
{
    public int schemaVersion = 2;
    public string localPlayerId;
    public string createdUtc;
    public string updatedUtc;
    public long revision;
    public string guestName = "Player";
    public int avatarIndex;
    public PlayerAudioPreferences settings = new PlayerAudioPreferences();
    public int wins;
    public int losses;
    public List<LocalMatchSummary> recentMatches = new List<LocalMatchSummary>();
    public int selectedDeck;
    public List<SavedDeck> decks = new List<SavedDeck>();
    public List<string> favoriteCardIds = new List<string>();
}

// This is only a local guest profile. It does not represent an online account.
public static class LocalDeckStore
{
    private const int SchemaVersion = 2;
    private const int MaxSavedDecks = 3;
    private const int MaxCopies = 3;
    private const string FileName = "local_decks_v1.json";

    private static readonly string[] StarterIds =
    {
        "sample_stone_guardian", "sample_stone_guardian", "sample_stone_guardian",
        "sample_tide_sprite", "sample_tide_sprite", "sample_tide_sprite",
        "dawnwing_fairy", "dawnwing_fairy", "dawnwing_fairy",
        "gravebound_imp", "gravebound_imp", "gravebound_imp",
        "fire_dragon", "fire_dragon", "sample_sky_sage", "legendary_fighter",
        "sample_arcane_insight", "sample_arcane_insight",
        "sample_quick_strike", "sample_quick_strike", "sample_iron_blade",
        "test_field_spell", "energy_venom", "power_transfer", "power_transfer",
        "temporal_lock", "sample_ambush", "sample_ambush",
        "sample_divine_reversal", "guardian_shift"
    };

    private static readonly string[] GuardIds =
    {
        "sample_stone_guardian", "sample_stone_guardian", "sample_stone_guardian",
        "sample_tide_sprite", "sample_tide_sprite", "sample_tide_sprite",
        "dawnwing_fairy", "dawnwing_fairy", "dawnwing_fairy",
        "gravebound_imp", "gravebound_imp", "gravebound_imp",
        "fire_dragon", "fire_dragon", "sample_sky_sage", "legendary_fighter",
        "sample_arcane_insight", "sample_arcane_insight", "sample_arcane_insight",
        "sample_iron_blade", "sample_iron_blade", "test_field_spell",
        "test_field_spell", "energy_venom", "energy_venom",
        "guardian_shift", "guardian_shift", "sample_ambush", "sample_ambush",
        "sample_divine_reversal", "temporal_lock"
    };

    public static int MaximumSavedDecks => MaxSavedDecks;
    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    public static LocalPlayerProfile Load(IReadOnlyDictionary<string, CardData> catalog,
        out string warning, string storagePath = null)
    {
        string savePath = storagePath ?? SavePath;
        warning = null;
        if (!File.Exists(savePath))
            return CreateDefault(catalog, out warning);

        try
        {
            LocalPlayerProfile profile = JsonUtility.FromJson<LocalPlayerProfile>(
                File.ReadAllText(savePath));
            if (profile == null || profile.schemaVersion < 1 || profile.schemaVersion > SchemaVersion ||
                profile.decks == null || profile.decks.Count == 0 ||
                profile.decks.Count > MaxSavedDecks)
                throw new InvalidDataException("Unsupported local deck save schema.");

            GameConfig config = GameConfig.CreateDefault();
            foreach (SavedDeck deck in profile.decks)
            {
                if (!Validate(deck, catalog, config, out string reason))
                    throw new InvalidDataException(reason);
            }

            if (profile.selectedDeck < 0 || profile.selectedDeck >= profile.decks.Count)
                profile.selectedDeck = 0;
            if (profile.favoriteCardIds == null)
                profile.favoriteCardIds = new List<string>();
            Normalize(profile);
            return profile;
        }
        catch (Exception exception)
        {
            warning = "Local deck save could not be loaded (" + exception.Message +
                "). It was left untouched; temporary preset decks are shown. " +
                "An explicit SAVE replaces it and keeps a .backup copy.";
            return CreateDefault(catalog, out _);
        }
    }

    public static bool Save(LocalPlayerProfile profile,
        IReadOnlyDictionary<string, CardData> catalog, out string error, string storagePath = null)
    {
        string savePath = storagePath ?? SavePath;
        error = null;
        if (profile == null || profile.decks == null || profile.decks.Count == 0 ||
            profile.decks.Count > MaxSavedDecks)
        {
            error = "Choose one to three decks to save.";
            return false;
        }

        GameConfig config = GameConfig.CreateDefault();
        foreach (SavedDeck deck in profile.decks)
        {
            if (!Validate(deck, catalog, config, out error))
                return false;
        }

        Normalize(profile);
        long previousRevision = profile.revision;
        string previousUpdated = profile.updatedUtc;
        profile.revision++;
        profile.updatedUtc = DateTime.UtcNow.ToString("o");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            string pending = savePath + ".pending";
            File.WriteAllText(pending, JsonUtility.ToJson(profile, true));
            if (File.Exists(savePath))
            {
                // Preserve the previous valid save if a replacement fails.
                File.Replace(pending, savePath, savePath + ".backup");
            }
            else
                File.Move(pending, savePath);
            return true;
        }
        catch (Exception exception)
        {
            profile.revision = previousRevision;
            profile.updatedUtc = previousUpdated;
            error = "Could not save local decks: " + exception.Message;
            return false;
        }
    }

    public static bool Validate(SavedDeck deck,
        IReadOnlyDictionary<string, CardData> catalog, GameConfig config,
        out string reason)
    {
        reason = null;
        if (deck == null || deck.cardIds == null ||
            deck.cardIds.Count < config.MinimumDeckSize ||
            deck.cardIds.Count > config.MaximumDeckSize)
        {
            reason = "A deck needs " + config.MinimumDeckSize + "-" +
                config.MaximumDeckSize + " cards.";
            return false;
        }

        Dictionary<string, int> copies = new Dictionary<string, int>();
        foreach (string id in deck.cardIds)
        {
            if (string.IsNullOrEmpty(id) || !catalog.ContainsKey(id))
            {
                reason = "Unknown card ID in deck: " + id;
                return false;
            }
            copies.TryGetValue(id, out int count);
            if (++count > MaxCopies)
            {
                reason = "More than three copies of " + catalog[id].CardName + ".";
                return false;
            }
            copies[id] = count;
        }
        return true;
    }

    public static bool CanAdd(SavedDeck deck, string cardId, out string reason)
    {
        reason = null;
        if (deck.cardIds.Count >= GameConfig.CreateDefault().MaximumDeckSize)
            reason = "Deck is full (40 cards).";
        else if (deck.cardIds.FindAll(id => id == cardId).Count >= MaxCopies)
            reason = "Maximum three copies per card.";
        return reason == null;
    }

    private static LocalPlayerProfile CreateDefault(
        IReadOnlyDictionary<string, CardData> catalog, out string warning)
    {
        warning = null;
        LocalPlayerProfile profile = new LocalPlayerProfile();
        Normalize(profile);
        AddPreset(profile, "Starter Balance", StarterIds, catalog);
        AddPreset(profile, "Guardian Control", GuardIds, catalog);
        if (profile.decks.Count == 0)
            warning = "Card catalog is incomplete; prepare sample cards in Edit Mode.";
        return profile;
    }

    public static LocalPlayerProfile CreatePreviewProfile(IReadOnlyDictionary<string, CardData> catalog)
        => CreateDefault(catalog, out _);

    private static void Normalize(LocalPlayerProfile profile)
    {
        profile.schemaVersion = SchemaVersion;
        if (!Guid.TryParseExact(profile.localPlayerId, "N", out _)) profile.localPlayerId = Guid.NewGuid().ToString("N");
        if (string.IsNullOrEmpty(profile.createdUtc)) profile.createdUtc = DateTime.UtcNow.ToString("o");
        if (profile.settings == null) profile.settings = new PlayerAudioPreferences();
        profile.settings.masterVolume = Mathf.Clamp01(profile.settings.masterVolume);
        profile.settings.musicVolume = Mathf.Clamp01(profile.settings.musicVolume);
        profile.settings.effectsVolume = Mathf.Clamp01(profile.settings.effectsVolume);
        if (profile.recentMatches == null) profile.recentMatches = new List<LocalMatchSummary>();
        profile.recentMatches.RemoveAll(item => item == null);
        if (profile.recentMatches.Count > 50) profile.recentMatches.RemoveRange(50, profile.recentMatches.Count - 50);
        if (profile.favoriteCardIds == null) profile.favoriteCardIds = new List<string>();
        profile.selectedDeck = Mathf.Clamp(profile.selectedDeck, 0, Mathf.Max(0, profile.decks.Count - 1));
        foreach (SavedDeck deck in profile.decks)
            if (string.IsNullOrEmpty(deck.deckId)) deck.deckId = Guid.NewGuid().ToString("N");
    }

    private static void AddPreset(LocalPlayerProfile profile, string name,
        string[] ids, IReadOnlyDictionary<string, CardData> catalog)
    {
        SavedDeck deck = new SavedDeck { deckId = Guid.NewGuid().ToString("N"), name = name,
            cardIds = new List<string>(ids) };
        if (Validate(deck, catalog, GameConfig.CreateDefault(), out _))
            profile.decks.Add(deck);
    }
}
