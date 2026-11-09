using UnityEngine;

// Build-only references to real card assets. No player profile is loaded or saved.
public sealed class SimulationCatalog : ScriptableObject
{
    public CardData[] cards;
    public SavedDeck[] presets;
    public string sourceRevision;
    public string sourceFingerprint;
    public string catalogFingerprint;
}
