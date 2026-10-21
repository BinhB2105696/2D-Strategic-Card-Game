using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Focused integration checks, using isolated temporary saves and a preview scene.
public static class AppFoundationVerification
{
    [MenuItem("Card Game/App Shell/Verify App Foundations")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            var catalog = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data/Cards" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToDictionary(card => card.CardId);
            string directory = Path.GetFullPath(Path.Combine("Temp", "AppFoundationVerification", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "profile.json");
            LocalPlayerProfile initial = LocalDeckStore.Load(catalog, out string warning, path);
            Require(warning == null && initial.decks.Count >= 2, "Default decks must be valid.");
            initial.guestName = "Verification";
            Require(LocalDeckStore.Save(initial, catalog, out string error, path), error);
            LocalPlayerProfile loaded = LocalDeckStore.Load(catalog, out warning, path);
            Require(warning == null && loaded.localPlayerId == initial.localPlayerId && loaded.guestName == "Verification", "Identity/profile did not round-trip.");
            string original = File.ReadAllText(path);
            loaded.decks[0].cardIds.AddRange(Enumerable.Repeat(loaded.decks[0].cardIds[0], 4));
            Require(!LocalDeckStore.Save(loaded, catalog, out _, path) && File.ReadAllText(path) == original, "Invalid copies changed the save.");
            string legacy = JsonUtility.ToJson(initial).Replace("\"schemaVersion\":2", "\"schemaVersion\":1");
            File.WriteAllText(path, legacy);
            loaded = LocalDeckStore.Load(catalog, out warning, path);
            Require(warning == null && loaded.schemaVersion == 2 && loaded.localPlayerId == initial.localPlayerId, "Version 1 migration failed.");
            Require(LocalDeckStore.Save(loaded, catalog, out error, path), error);
            Require(File.Exists(path + ".backup"), "Replacement save must keep a backup.");
            File.WriteAllText(path, "{broken json");
            LocalDeckStore.Load(catalog, out warning, path);
            Require(!string.IsNullOrEmpty(warning) && File.ReadAllText(path) == "{broken json", "Corrupt save was overwritten.");
            string future = JsonUtility.ToJson(initial).Replace("\"schemaVersion\":2", "\"schemaVersion\":999");
            File.WriteAllText(path, future);
            LocalDeckStore.Load(catalog, out warning, path);
            Require(!string.IsNullOrEmpty(warning) && File.ReadAllText(path) == future, "Future schema was overwritten.");
            foreach (SavedDeck deck in initial.decks) VerifySession(deck, initial.decks[0], catalog);
            SavedDeck forty = new SavedDeck { name = "Forty card verification", cardIds = new List<string>(initial.decks[0].cardIds) };
            foreach (string id in catalog.Keys)
            {
                while (forty.cardIds.Count < 40 && LocalDeckStore.CanAdd(forty, id, out _)) forty.cardIds.Add(id);
            }
            Require(forty.cardIds.Count == 40, "Could not assemble a 40-card legal test deck.");
            VerifySession(forty, initial.decks[0], catalog);
            VerifyUi();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/AppFoundationVerification.txt", DateTime.UtcNow.ToString("o") +
                "\nPASS: save round-trip, v1 migration, copy rejection, backup, corrupt/future preservation, 30/31/40-card sessions and Easy Bot decisions, authored UI bindings/pages.\nTemporary data: " + directory);
            Debug.Log("App foundations verification PASSED. Report: Logs/AppFoundationVerification.txt");
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/AppFoundationVerification.txt", "FAIL\n" + exception);
            Debug.LogException(exception);
        }
    }

    private static void VerifySession(SavedDeck chosen, SavedDeck opponent, Dictionary<string, CardData> catalog)
    {
        Require(LocalDeckStore.Validate(chosen, catalog, GameConfig.CreateDefault(), out string reason), reason);
        GameSession session = GameSession.Create(GameConfig.CreateDefault(), 20261021,
            chosen.cardIds.Select(id => CardDataConverter.ToDefinition(catalog[id])).ToList(),
            opponent.cardIds.Select(id => CardDataConverter.ToDefinition(catalog[id])).ToList());
        Require(session.State.Players[0].Hand.Count + session.State.Players[0].Deck.Count == chosen.cardIds.Count,
            "Selected deck was truncated during startup.");
        EasyBot bot = new EasyBot();
        for (int step = 0; step < 150 && !session.State.IsFinished; step++)
        {
            int player = session.State.InteractionPlayerIndex;
            GameAction action;
            if (!session.State.HasOpenResponseWindow && !session.State.HasPendingTriggeredEffect && session.State.CurrentPhase == TurnPhase.Recovery)
                action = session.State.CurrentRecoveryStep == RecoveryStep.Draw
                    ? GameAction.ResolveDrawStep(player) : GameAction.ResolveEnergyRefreshStep(player);
            else if (!session.State.HasOpenResponseWindow && !session.State.HasPendingTriggeredEffect && session.State.CurrentPhase == TurnPhase.Standby)
                action = GameAction.AdvancePhase(player);
            else
            {
                IReadOnlyList<GameAction> legal = GameRules.GetLegalActions(session.State, player);
                Require(legal.Count > 0, "No legal action during selected-deck integration check.");
                action = bot.ChooseAction(BotGameStateView.Create(session.State, player), legal);
            }
            ActionResult result = session.TryApply(action);
            Require(result.Succeeded, result.ErrorMessage);
        }
    }

    private static void VerifyUi()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene app = SceneManager.GetSceneByPath("Assets/Scenes/AppShell.unity");
        bool opened = !app.IsValid() || !app.isLoaded;
        if (opened) app = EditorSceneManager.OpenScene("Assets/Scenes/AppShell.unity", OpenSceneMode.Additive);
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            DeckBuilderAppController source = app.GetRootGameObjects().Select(go => go.GetComponent<DeckBuilderAppController>()).First(c => c != null);
            GameObject copy = UnityEngine.Object.Instantiate(source.gameObject);
            SceneManager.MoveGameObjectToScene(copy, preview);
            copy.GetComponent<DeckBuilderAppController>().VerifyPreparedUi();
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(app, true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
