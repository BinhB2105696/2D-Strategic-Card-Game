using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PlayerAudioPreferences
{
    public float masterVolume = 1;
    public float musicVolume = .7f;
    public float effectsVolume = 1;
}

[Serializable]
public sealed class LocalMatchSummary
{
    public string matchId;
    public string finishedUtc;
    public string deckId;
    public string deckName;
    public string outcome;
    public string opponent = "Easy Bot";
    public int turns;
    public bool usedAuto;
    public uint seed;
}

// UI depends on this boundary. A future signed-in service must map an authenticated
// server identity to a local profile; the local ID is never proof of ownership.
public interface IPlayerProfileRepository
{
    LocalPlayerProfile Load(IReadOnlyDictionary<string, CardData> catalog, out string warning);
    bool Save(LocalPlayerProfile profile, IReadOnlyDictionary<string, CardData> catalog, out string error);
}

public sealed class LocalPlayerProfileRepository : IPlayerProfileRepository
{
    public LocalPlayerProfile Load(IReadOnlyDictionary<string, CardData> catalog, out string warning)
        => LocalDeckStore.Load(catalog, out warning);
    public bool Save(LocalPlayerProfile profile, IReadOnlyDictionary<string, CardData> catalog, out string error)
        => LocalDeckStore.Save(profile, catalog, out error);
}

public static class AppProfileServices
{
    public static IPlayerProfileRepository Profiles { get; private set; } = new LocalPlayerProfileRepository();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Profiles = new LocalPlayerProfileRepository();
}

public static class AppAudioSettings
{
    public static event Action Changed;
    public static float MusicVolume { get; private set; } = .7f;
    public static float EffectsVolume { get; private set; } = 1;

    public static void Apply(PlayerAudioPreferences settings)
    {
        AudioListener.volume = Mathf.Clamp01(settings.masterVolume);
        MusicVolume = Mathf.Clamp01(settings.musicVolume);
        EffectsVolume = Mathf.Clamp01(settings.effectsVolume);
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Changed = null;
        MusicVolume = .7f;
        EffectsVolume = 1;
        AudioListener.volume = 1;
    }
}
