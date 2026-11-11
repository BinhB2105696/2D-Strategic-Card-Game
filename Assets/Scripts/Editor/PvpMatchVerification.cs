using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Focused authority/privacy checks; no sign-in, live network traffic or save writes.
public static class PvpMatchVerification
{
    [MenuItem("Card Game/Networking/Verify PvP Match Adapter")]
    public static void Verify()
    {
        int checks = PvpAuthorityChecks.Run();
        var app = UnityEngine.Object.FindObjectOfType<DeckBuilderAppController>();
        if (app == null) throw new InvalidOperationException("Open AppShell in Edit Mode first.");
        var serialized = new SerializedObject(app);
        SerializedProperty catalog = serialized.FindProperty("catalogCards");
        var cards = new List<CardDefinition>();
        for (int index = 0; index < catalog.arraySize; index++)
        {
            var card = catalog.GetArrayElementAtIndex(index).objectReferenceValue as CardData;
            if (card != null) cards.Add(CardDataConverter.ToDefinition(card));
        }
        if (cards.Count < 10) throw new InvalidOperationException("AppShell card catalog is missing.");
        string fingerprint = PvpCatalogFingerprint.Compute(cards);
        if (fingerprint != PvpCatalogFingerprint.Compute(cards.AsEnumerable().Reverse()))
            throw new InvalidOperationException("Catalog hashing depends on asset ordering.");
        Debug.Log("PvP match adapter PASS: " + checks + " authority/privacy checks; " + cards.Count +
            " real card definitions compatible. No online connectivity test was performed.");
    }
}
