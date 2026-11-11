using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class DeckBuilderAppController
{
    private PvpOfficialDuelView officialPvp;
    private bool loadingPvp;
    private Scene pvpScene;

    private void RefreshPvpBattle(PvpMatchView view)
    {
        if (officialPvp != null) { officialPvp.Receive(view); return; }
        if (!loadingPvp) StartCoroutine(LoadOfficialPvp());
    }

    private IEnumerator LoadOfficialPvp()
    {
        loadingPvp = true;
        SceneManager.sceneLoaded += OfficialPvpLoaded;
        AsyncOperation operation = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Additive);
        yield return operation;
        SceneManager.sceneLoaded -= OfficialPvpLoaded;
        loadingPvp = false;
    }

    private void OfficialPvpLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "SampleScene" || mode != LoadSceneMode.Additive) return;
        pvpScene = scene;
        var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
        // sceneLoaded is raised after Awake and before Start. Suppress the local session/bot.
        foreach (MonoBehaviour component in components)
            if (component is DuelBoardReadOnlyDemo || component is DuelEasyBotController ||
                component is DuelHudPrototypeController || component is DuelCardInteractionController ||
                component is DuelPresentationController || component is EventSystem || component is BaseInputModule)
                component.enabled = false;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        try
        {
            DuelBoardReadOnlyDemo board = components.OfType<DuelBoardReadOnlyDemo>().First();
            officialPvp = board.gameObject.AddComponent<PvpOfficialDuelView>();
            officialPvp.Configure(board, pvpDuel, pvpRoom, byId, profile, CurrentAvatar(), avatarSprites, duelOverlayPrefab,
                () => StartCoroutine(ReturnFromOfficialPvp()));
            canvas.enabled = false;
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
            officialPvp.Receive(pvpDuel.View);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            pvpDuel.Surrender(); pvpRoom.Leave();
            if (scene.IsValid()) SceneManager.UnloadSceneAsync(scene);
            officialPvp = null;
            SetMessage("Could not prepare the official PvP board: " + exception.Message);
        }
    }

    private IEnumerator ReturnFromOfficialPvp()
    {
        PvpMatchView view = pvpDuel.View;
        if (view == null || !pvpDuel.CanLeaveResult) yield break;
        if (!PvpLocalHistory.Save(view, out string error)) { officialPvp.ShowSaveError(error); yield break; }
        pvpRoom.Leave(); pvpDuel.ClearFinished();
        if (pvpScene.IsValid() && pvpScene.isLoaded) yield return SceneManager.UnloadSceneAsync(pvpScene);
        officialPvp = null;
        canvas.enabled = true;
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = true;
        OpenModalPanel(pvpPanel);
    }
}
