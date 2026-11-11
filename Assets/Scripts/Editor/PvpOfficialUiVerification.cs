using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds a disposable clone of the authored PvE UI. Never saves scenes/assets or signs in.
public static class PvpOfficialUiVerification
{
    [MenuItem("Card Game/Networking/Verify Official PvP UI")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run the UI check in Edit Mode.");
        var app = UnityEngine.Object.FindObjectOfType<DeckBuilderAppController>();
        if (app == null) throw new InvalidOperationException("Open AppShell first.");
        var serialized = new SerializedObject(app);
        SerializedProperty source = serialized.FindProperty("catalogCards");
        var catalog = new Dictionary<string, CardData>();
        for (int index = 0; index < source.arraySize; index++)
        {
            var card = source.GetArrayElementAtIndex(index).objectReferenceValue as CardData;
            if (card != null) catalog[card.CardId] = card;
        }
        var overlay = serialized.FindProperty("duelOverlayPrefab").objectReferenceValue as GameObject;
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var original = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DuelBoardReadOnlyDemo>(true)).First();
            GameObject clone = UnityEngine.Object.Instantiate(original.gameObject);
            SceneManager.MoveGameObjectToScene(clone, preview);
            foreach (MonoBehaviour component in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is DuelBoardReadOnlyDemo || component is DuelEasyBotController ||
                    component is DuelHudPrototypeController || component is DuelCardInteractionController ||
                    component is DuelPresentationController) component.enabled = false;
            var helper = new GameObject("PvpPreviewServices"); SceneManager.MoveGameObjectToScene(helper, preview);
            var room = helper.AddComponent<PvpRoomConnection>(); var coordinator = helper.AddComponent<PvpDuelCoordinator>();
            string[] deck = catalog.Keys.OrderBy(id => id, StringComparer.Ordinal).Take(10)
                .SelectMany(id => Enumerable.Repeat(id, 3)).ToArray();
            var profile = new LocalPlayerProfile { decks = new List<SavedDeck> { new SavedDeck { name = "Preview", cardIds = deck.ToList() } } };
            CardDefinition[] definitions = catalog.Values.Select(CardDataConverter.ToDefinition).ToArray();
            var authority = new PvpDuelAuthority(definitions, PvpCatalogFingerprint.Compute(definitions), 42, ids => null);
            string fingerprint = PvpCatalogFingerprint.Compute(definitions);
            authority.Ready(0, fingerprint, "Preview A", deck); authority.Ready(1, fingerprint, "Preview B", deck);
            authority.BoardReady(0); authority.BoardReady(1);
            var view = clone.AddComponent<PvpOfficialDuelView>();
            view.Configure(clone.GetComponent<DuelBoardReadOnlyDemo>(), coordinator, room, catalog, profile, null, null, overlay, () => { });
            PvpMatchView received = JsonUtility.FromJson<PvpMatchMessage>(JsonUtility.ToJson(
                new PvpMatchMessage { kind = "view", view = authority.View(0) })).view;
            if (received.choices == null || received.seconds == null || received.seconds.Length != 2 || received.query == null)
                throw new InvalidOperationException("The clock/action snapshot failed its native JSON round-trip.");
            view.VerifyPreviewLayout(received);
            if (clone.GetComponent<DuelBoardReadOnlyDemo>().Session != null)
                throw new InvalidOperationException("PvP presentation accidentally created a local GameSession.");
            Transform timer = clone.transform.Find("SafeAreaRoot/HudLayer/PvpTurnTimer");
            if (timer == null || timer.GetComponent<Button>().enabled)
                throw new InvalidOperationException("The turn clock did not replace Auto.");
            if (clone.GetComponentsInChildren<Button>(true).Any(button => button.enabled && button.name.Contains("AutoPlay")))
                throw new InvalidOperationException("An Auto control remains active in PvP.");
            Transform hand = clone.transform.Find("SafeAreaRoot/BoardLayer/Player_HandArea/HandContent");
            Button firstCard = hand.GetComponentsInChildren<Button>(false).FirstOrDefault();
            if (firstCard == null) throw new InvalidOperationException("Authored hand did not render network cards.");
            if (firstCard.targetGraphic == null || !firstCard.targetGraphic.raycastTarget)
                throw new InvalidOperationException("Network cards have no touch/raycast surface.");
            Transform blocker = clone.transform.Find("SafeAreaRoot/OverlayLayer/RuntimePresentationLayer/PresentationInputBlocker");
            if (blocker != null && blocker.gameObject.activeSelf)
                throw new InvalidOperationException("The shared presentation blocker hides all network input.");
            firstCard.onClick.Invoke();
            if (!clone.transform.Find("SafeAreaRoot/OverlayLayer/CardDetailPanel").gameObject.activeSelf)
                throw new InvalidOperationException("The original card-information panel did not open.");
            Debug.Log("Official PvP UI PASS: authored board/cards/panels, clock replacing Auto, no local session, card information. Device/visual acceptance pending.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
