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
    public int presetDeckVersion;
    public List<SavedDeck> decks = new List<SavedDeck>();
    public List<string> favoriteCardIds = new List<string>();
}

// This is only a local guest profile. It does not represent an online account.
public static class LocalDeckStore
{
    private const int SchemaVersion = 2;
    private const int MaxSavedDecks = 6;
    private const int PresetDeckVersion = 4;
    private const int MaxCopies = 3;
    private const string FileName = "local_decks_v1.json";

    private static readonly string[] StarterIds =
    {
        "dawnwing_fairy", "dawnwing_fairy", "bloodfang_raider", "bloodfang_raider",
        "battle_herald", "logistics_squire", "siegebreaker", "fire_dragon",
        "arcane_archivist", "sample_sky_sage", "sample_stone_guardian", "sample_stone_guardian",
        "bastion_keeper", "energy_wisp", "sample_tide_sprite", "thorn_bastion", "aegis_sentinel",
        "sample_arcane_insight", "sample_arcane_insight",
        "sample_quick_strike", "stop_defense", "energy_reserve", "dispel_magic",
        "elite_summons", "battlefield_medicine", "test_field_spell",
        "sample_ambush", "sample_ambush",
        "sample_divine_reversal", "guardian_shift"
    };

    private static readonly string[] AssaultIds =
    {
        "dawnwing_fairy", "dawnwing_fairy", "dawnwing_fairy",
        "bloodfang_raider", "bloodfang_raider", "bloodfang_raider",
        "battle_herald", "battle_herald", "logistics_squire", "logistics_squire",
        "siegebreaker", "siegebreaker", "scar_duelist", "fire_dragon",
        "arcane_archivist", "sample_sky_sage",
        "sample_quick_strike", "sample_quick_strike", "stop_defense", "stop_defense",
        "energy_reserve", "energy_reserve", "sample_arcane_insight", "sample_arcane_insight",
        "sample_iron_blade", "dispel_magic", "power_transfer", "elite_summons",
        "sample_ambush", "sample_divine_reversal"
    };

    private static readonly string[] GuardIds =
    {
        "dawnwing_fairy", "dawnwing_fairy", "arcane_archivist", "sample_sky_sage",
        "sample_stone_guardian", "sample_stone_guardian", "bastion_keeper", "bastion_keeper",
        "energy_wisp", "energy_wisp", "field_medic", "sample_tide_sprite",
        "ambush_lancer", "thorn_bastion", "aegis_sentinel", "aegis_sentinel",
        "stop_defense", "energy_reserve", "sample_arcane_insight", "sample_arcane_insight",
        "dispel_magic", "elite_summons", "emergency_reinforcements", "defensive_outpost",
        "recover_the_fallen", "sample_ambush", "sample_ambush", "sample_divine_reversal",
        "guardian_shift", "guardian_shift"
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
            AddStrategicPresets(profile, catalog);
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
            error = "Choose one to " + MaxSavedDecks + " decks to save.";
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
            if (++count > catalog[id].MaxCopiesPerDeck)
            {
                reason = "Maximum " + catalog[id].MaxCopiesPerDeck + " copies of " + catalog[id].CardName + ".";
                return false;
            }
            copies[id] = count;
        }
        return true;
    }

    public static bool CanAdd(SavedDeck deck, string cardId, out string reason, int maxCopies = MaxCopies)
    {
        reason = null;
        if (deck.cardIds.Count >= GameConfig.CreateDefault().MaximumDeckSize)
            reason = "Deck is full (40 cards).";
        else if (deck.cardIds.FindAll(id => id == cardId).Count >= maxCopies)
            reason = "Maximum " + maxCopies + " copies per card.";
        return reason == null;
    }

    private static LocalPlayerProfile CreateDefault(
        IReadOnlyDictionary<string, CardData> catalog, out string warning)
    {
        warning = null;
        LocalPlayerProfile profile = new LocalPlayerProfile();
        Normalize(profile);
        AddStrategicPresets(profile, catalog);
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

    private static void AddStrategicPresets(LocalPlayerProfile profile,
        IReadOnlyDictionary<string, CardData> catalog)
    {
        if (profile.presetDeckVersion >= PresetDeckVersion ||
            profile.decks.Count + 3 > MaxSavedDecks) return;

        SavedDeck[] presets =
        {
            new SavedDeck { name = "Starter Balance v4", cardIds = new List<string>(StarterIds) },
            new SavedDeck { name = "Crimson Assault", cardIds = new List<string>(AssaultIds) },
            new SavedDeck { name = "Aegis Bastion", cardIds = new List<string>(GuardIds) }
        };
        foreach (SavedDeck deck in presets)
            if (!Validate(deck, catalog, GameConfig.CreateDefault(), out _)) return;

        // Append once; preserve the player's existing decks, selection and profile data.
        foreach (SavedDeck deck in presets)
        {
            deck.deckId = Guid.NewGuid().ToString("N");
            profile.decks.Add(deck);
        }
        profile.presetDeckVersion = PresetDeckVersion;
    }
}
