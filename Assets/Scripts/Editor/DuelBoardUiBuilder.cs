using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DuelBoardUiBuilder
{
    private const string AutoBuildSessionKey =
        "CardGame.DuelBoardUiBuilder.AutoBuild.2026-10-06.FontFix";

    private static readonly Color32 DarkPanel = new Color32(7, 21, 51, 242);
    private static readonly Color32 DarkPanelSoft = new Color32(12, 34, 75, 220);
    private static readonly Color32 Blue = new Color32(0, 95, 190, 240);
    private static readonly Color32 Cyan = new Color32(40, 205, 235, 255);
    private static readonly Color32 Yellow = new Color32(232, 183, 38, 235);
    private static readonly Color32 Red = new Color32(185, 35, 35, 245);
    private static readonly Color32 White = new Color32(255, 255, 255, 255);

    private static TMP_FontAsset titleFont;
    private static TMP_FontAsset bodyFont;
    private static int createdObjectCount;

    [InitializeOnLoadMethod]
    private static void QueueAutomaticBuild()
    {
        EditorApplication.delayCall += TryAutomaticBuild;
    }

    private static void TryAutomaticBuild()
    {
        if (SessionState.GetBool(AutoBuildSessionKey, false) ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "SampleScene")
        {
            return;
        }

        if (FindInActiveScene("Field_Canvas") == null)
        {
            return;
        }

        BuildRemainingDuelUi();
        SessionState.SetBool(AutoBuildSessionKey, true);
    }

    [MenuItem("Card Game/Board UI/Build Remaining Duel UI")]
    public static void BuildRemainingDuelUi()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play Mode before building the duel UI.");
            return;
        }

        RectTransform canvas = FindInActiveScene("Field_Canvas");
        RectTransform boardLayer = FindInActiveScene("BoardLayer");
        RectTransform hudLayer = FindInActiveScene("HudLayer");
        RectTransform overlayLayer = FindInActiveScene("OverlayLayer");

        if (canvas == null || boardLayer == null || hudLayer == null ||
            overlayLayer == null)
        {
            Debug.LogError(
                "Duel UI builder requires Field_Canvas with BoardLayer, " +
                "HudLayer, and OverlayLayer in the active scene.");
            return;
        }

        titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/TMP/Cinzel-SemiBold SDF.asset");
        bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/TMP/EBGaramond-Regular SDF.asset");
        createdObjectCount = 0;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Remaining Duel UI");

        BuildHandArea(
            boardLayer,
            "Player_HandArea",
            new Vector2(0.5f, 0f),
            new Vector2(-70f, 140f),
            false);
        BuildHandArea(
            boardLayer,
            "Opp_HandArea",
            new Vector2(0.5f, 1f),
            new Vector2(70f, -140f),
            true);

        BuildPlayerHud(hudLayer);
        BuildOpponentHud(hudLayer);
        RectTransform turnPhaseHud = BuildTurnPhaseHud(hudLayer);
        Button settingsButton = BuildSettingsButton(hudLayer);
        PhaseUiReferences phaseUi = BuildPhaseControl(hudLayer);
        SettingsUiReferences settingsUi = BuildSettingsOverlay(overlayLayer);
        ConfirmationUiReferences confirmationUi =
            BuildConfirmationOverlay(overlayLayer);
        BuildFutureInteractionPanels(overlayLayer);

        RectTransform controllerRoot = GetOrCreateRect(
            hudLayer,
            "DuelHudPrototypeController",
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero);
        DuelHudPrototypeController controller =
            GetOrAddComponent<DuelHudPrototypeController>(controllerRoot.gameObject);

        controller.Configure(
            phaseUi.MenuButton,
            phaseUi.Choices,
            phaseUi.BattleButton,
            phaseUi.EndButton,
            settingsButton,
            settingsUi.CloseButton,
            settingsUi.Overlay,
            settingsUi.DuelLogTabButton,
            settingsUi.DeckInfoTabButton,
            settingsUi.SettingsTabButton,
            settingsUi.DuelLogContent,
            settingsUi.DeckInfoContent,
            settingsUi.SettingsContent,
            settingsUi.SurrenderButton,
            confirmationUi.Popup,
            confirmationUi.MessageText,
            confirmationUi.AcceptButton,
            confirmationUi.CancelButton);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(turnPhaseHud);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Undo.CollapseUndoOperations(undoGroup);

        Selection.activeGameObject = turnPhaseHud.gameObject;
        Debug.Log(
            "Remaining duel UI created without changing existing Deck, " +
            "Graveyard, Field, Energy, or Player_HandArea RectTransforms. " +
            "New objects: " + createdObjectCount +
            ". Review the Game view and save the scene when satisfied.");
    }

    private static void BuildHandArea(
        RectTransform boardLayer,
        string areaName,
        Vector2 anchor,
        Vector2 defaultPosition,
        bool isOpponent)
    {
        RectTransform handArea = FindDirectChild(boardLayer, areaName);
        if (handArea == null)
        {
            handArea = GetOrCreateRect(
                boardLayer,
                areaName,
                anchor,
                anchor,
                new Vector2(0.5f, isOpponent ? 1f : 0f),
                new Vector2(900f, 280f),
                defaultPosition);
        }

        RectTransform content = GetOrCreateRect(
            handArea,
            "HandContent",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f),
            Vector2.zero);

        HorizontalLayoutGroup layout =
            GetOrAddComponent<HorizontalLayoutGroup>(content.gameObject);
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = -50f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childScaleWidth = false;
        layout.childScaleHeight = false;

        ContentSizeFitter fitter =
            GetOrAddComponent<ContentSizeFitter>(content.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform slot = GetOrCreateRect(
            content,
            "HandCardSlot_Template",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(175f, 245f),
            Vector2.zero);
        LayoutElement layoutElement =
            GetOrAddComponent<LayoutElement>(slot.gameObject);
        layoutElement.preferredWidth = 175f;
        layoutElement.preferredHeight = 245f;

        RectTransform cardAnchor = GetOrCreateRect(
            slot,
            "CardAnchor",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(250f, 350f),
            Vector2.zero);
        cardAnchor.localScale = new Vector3(0.7f, 0.7f, 1f);
        slot.gameObject.SetActive(false);
    }

    private static void BuildPlayerHud(RectTransform hudLayer)
    {
        RectTransform root = GetOrCreateRect(
            hudLayer,
            "Player_HUD",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(430f, 190f),
            new Vector2(-20f, 20f));

        CreateImage(
            root,
            "AvatarFrame",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(170f, 170f),
            Vector2.zero,
            new Color32(30, 89, 145, 230));
        CreateText(
            root,
            "AvatarPlaceholderText",
            "PLAYER",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(170f, 170f),
            Vector2.zero,
            28f,
            TextAlignmentOptions.Center,
            titleFont);
        CreateText(
            root,
            "NameText",
            "Player",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(245f, 44f),
            new Vector2(-180f, 125f),
            30f,
            TextAlignmentOptions.Right,
            titleFont);
        CreateText(
            root,
            "HPText",
            "HP 4000",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(270f, 72f),
            new Vector2(-180f, 45f),
            48f,
            TextAlignmentOptions.Right,
            titleFont);
    }

    private static void BuildOpponentHud(RectTransform hudLayer)
    {
        RectTransform root = GetOrCreateRect(
            hudLayer,
            "Opp_HUD",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(430f, 190f),
            new Vector2(20f, -20f));

        CreateImage(
            root,
            "AvatarFrame",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(170f, 170f),
            Vector2.zero,
            new Color32(145, 45, 60, 230));
        CreateText(
            root,
            "AvatarPlaceholderText",
            "OPP",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(170f, 170f),
            Vector2.zero,
            28f,
            TextAlignmentOptions.Center,
            titleFont);
        CreateText(
            root,
            "NameText",
            "Opponent",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(245f, 44f),
            new Vector2(180f, -45f),
            30f,
            TextAlignmentOptions.Left,
            titleFont);
        CreateText(
            root,
            "HPText",
            "HP 4000",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(270f, 72f),
            new Vector2(180f, -125f),
            48f,
            TextAlignmentOptions.Left,
            titleFont);
    }

    private static RectTransform BuildTurnPhaseHud(RectTransform hudLayer)
    {
        RectTransform root = GetOrCreateRect(
            hudLayer,
            "TurnPhaseHUD",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(360f, 130f),
            new Vector2(-110f, -20f));
        CreateImage(root, "Background", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, DarkPanelSoft);
        CreateText(
            root,
            "TurnText",
            "Turn 1",
            new Vector2(0f, 0.5f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-20f, -8f),
            new Vector2(0f, -4f),
            40f,
            TextAlignmentOptions.Center,
            titleFont);
        CreateText(
            root,
            "PhaseText",
            "Recovery Phase",
            new Vector2(0f, 0f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-20f, -8f),
            new Vector2(0f, 4f),
            30f,
            TextAlignmentOptions.Center,
            titleFont,
            Yellow);
        return root;
    }

    private static Button BuildSettingsButton(RectTransform hudLayer)
    {
        Button button = CreateButton(
            hudLayer,
            "SettingsButton",
            string.Empty,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(90f, 90f),
            new Vector2(-10f, -20f),
            Blue,
            24f);
        RectTransform root = button.GetComponent<RectTransform>();
        CreateImage(root, "MenuBarTop", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(48f, 7f), new Vector2(0f, 16f), White);
        CreateImage(root, "MenuBarMiddle", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(48f, 7f), Vector2.zero, White);
        CreateImage(root, "MenuBarBottom", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(48f, 7f), new Vector2(0f, -16f), White);
        return button;
    }

    private static PhaseUiReferences BuildPhaseControl(RectTransform hudLayer)
    {
        RectTransform root = GetOrCreateRect(
            hudLayer,
            "PhaseControl",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(280f, 300f),
            new Vector2(-20f, 0f));

        Button menuButton = CreateButton(
            root,
            "PhaseMenuButton",
            "PHASE",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(150f, 100f),
            Vector2.zero,
            new Color32(65, 165, 225, 245),
            28f);

        RectTransform choices = GetOrCreateRect(
            root,
            "PhaseChoices",
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        Button battleButton = CreateButton(
            choices,
            "BattlePhaseButton",
            "BATTLE PHASE",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(240f, 76f),
            new Vector2(0f, 95f),
            Yellow,
            27f);
        Button endButton = CreateButton(
            choices,
            "EndPhaseButton",
            "END PHASE",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(240f, 76f),
            new Vector2(0f, -95f),
            Red,
            27f);
        choices.gameObject.SetActive(false);

        return new PhaseUiReferences
        {
            MenuButton = menuButton,
            Choices = choices.gameObject,
            BattleButton = battleButton,
            EndButton = endButton
        };
    }

    private static SettingsUiReferences BuildSettingsOverlay(RectTransform overlayLayer)
    {
        RectTransform overlay = GetOrCreateRect(
            overlayLayer,
            "SettingsOverlay",
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        RectTransform settingsDim = CreateImage(
            overlay, "ScreenDim", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            new Color32(0, 0, 0, 170));
        settingsDim.GetComponent<Image>().raycastTarget = true;

        RectTransform panel = CreateImage(
            overlay,
            "Panel",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(980f, 1600f),
            Vector2.zero,
            DarkPanel);
        CreateText(panel, "TitleText", "DUEL MENU",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(500f, 70f),
            new Vector2(0f, -25f), 46f, TextAlignmentOptions.Center, titleFont);

        RectTransform summary = CreateImage(
            panel,
            "DuelSummary",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(920f, 250f),
            new Vector2(0f, -105f),
            DarkPanelSoft);
        CreateText(summary, "OpponentHpText", "Opponent  HP 4000",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(390f, 70f), new Vector2(25f, -25f), 34f,
            TextAlignmentOptions.Left, titleFont);
        CreateText(summary, "PlayerHpText", "Player  HP 4000",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(390f, 70f), new Vector2(-25f, -25f), 34f,
            TextAlignmentOptions.Right, titleFont);
        CreateText(summary, "TurnPhaseText", "Turn 1  -  Recovery Phase",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(650f, 60f),
            new Vector2(0f, -90f), 32f, TextAlignmentOptions.Center,
            titleFont, Yellow);
        Button surrenderButton = CreateButton(
            summary,
            "SurrenderButton",
            "SURRENDER",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(500f, 76f),
            new Vector2(0f, 22f),
            Red,
            32f);

        RectTransform tabs = GetOrCreateRect(
            panel, "Tabs", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(920f, 100f),
            new Vector2(0f, -375f));
        Button duelLogTab = CreateButton(tabs, "DuelLogTabButton", "DUEL LOG",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(285f, 90f),
            new Vector2(-305f, 0f), Blue, 28f);
        Button deckInfoTab = CreateButton(tabs, "DeckInfoTabButton", "DECK INFO",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(285f, 90f),
            Vector2.zero, Blue, 28f);
        Button settingsTab = CreateButton(tabs, "DuelSettingsTabButton", "SETTINGS",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(285f, 90f),
            new Vector2(305f, 0f), Blue, 28f);

        RectTransform contentPanel = CreateImage(
            panel, "ContentPanel", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(920f, 950f), new Vector2(0f, -115f),
            new Color32(4, 15, 43, 245));
        RectTransform duelLogContent = CreateTabContent(
            contentPanel,
            "DuelLogContent",
            "DUEL LOG\n\nTurn 1 — Player draws 1 card.\n" +
            "Player refreshes Energy to 5.\n" +
            "Player summons a Monster.\n\n" +
            "Runtime entries will be generated from MatchLog and GameEvent.");
        RectTransform deckInfoContent = CreateTabContent(
            contentPanel,
            "DeckInfoContent",
            "DECK INFO\n\nDeck name, remaining cards, Graveyard, " +
            "and revealed-card information will appear here.");
        RectTransform duelSettingsContent = CreateTabContent(
            contentPanel,
            "DuelSettingsContent",
            "DUEL SETTINGS\n\nAnimation speed, audio, confirmation, " +
            "and accessibility options will appear here.");
        duelLogContent.gameObject.SetActive(true);
        deckInfoContent.gameObject.SetActive(false);
        duelSettingsContent.gameObject.SetActive(false);

        Button closeButton = CreateButton(
            panel,
            "CloseButton",
            "BACK",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(180f, 80f),
            new Vector2(30f, 30f),
            Blue,
            30f);
        overlay.gameObject.SetActive(false);

        return new SettingsUiReferences
        {
            Overlay = overlay.gameObject,
            CloseButton = closeButton,
            DuelLogTabButton = duelLogTab,
            DeckInfoTabButton = deckInfoTab,
            SettingsTabButton = settingsTab,
            DuelLogContent = duelLogContent.gameObject,
            DeckInfoContent = deckInfoContent.gameObject,
            SettingsContent = duelSettingsContent.gameObject,
            SurrenderButton = surrenderButton
        };
    }

    private static ConfirmationUiReferences BuildConfirmationOverlay(
        RectTransform overlayLayer)
    {
        RectTransform popup = GetOrCreateRect(
            overlayLayer,
            "PhaseConfirmationPopup",
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        RectTransform confirmationDim = CreateImage(
            popup, "ScreenDim", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            new Color32(0, 0, 0, 175));
        confirmationDim.GetComponent<Image>().raycastTarget = true;
        RectTransform box = CreateImage(
            popup,
            "ConfirmationBox",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(760f, 420f),
            Vector2.zero,
            DarkPanel);
        TMP_Text message = CreateText(
            box,
            "MessageText",
            "Confirm this action?",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(680f, 190f),
            new Vector2(0f, -45f),
            36f,
            TextAlignmentOptions.Center,
            bodyFont);
        Button accept = CreateButton(
            box, "ConfirmButton", "CONFIRM",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(280f, 86f),
            new Vector2(165f, 45f), Blue, 29f);
        Button cancel = CreateButton(
            box, "CancelButton", "CANCEL",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(280f, 86f),
            new Vector2(-165f, 45f), new Color32(80, 90, 115, 245), 29f);
        popup.gameObject.SetActive(false);

        return new ConfirmationUiReferences
        {
            Popup = popup.gameObject,
            MessageText = message,
            AcceptButton = accept,
            CancelButton = cancel
        };
    }

    private static void BuildFutureInteractionPanels(RectTransform overlayLayer)
    {
        RectTransform detail = CreateImage(
            overlayLayer,
            "CardDetailPanel",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(900f, 1400f),
            Vector2.zero,
            DarkPanel);
        CreateText(detail, "PlaceholderText",
            "CARD DETAILS\n\nArtwork, attributes, current stats, " +
            "allocated Energy, and full effect text will appear here.",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            new Vector2(-80f, -80f), Vector2.zero, 40f,
            TextAlignmentOptions.Center, bodyFont);
        detail.gameObject.SetActive(false);

        RectTransform actions = CreateImage(
            overlayLayer,
            "CardActionPanel",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(900f, 260f),
            new Vector2(0f, 30f),
            DarkPanel);
        CreateText(actions, "PlaceholderText",
            "Legal card actions will appear here: Summon, Activate, Set, " +
            "Allocate Energy, Change Position, or Attack.",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            new Vector2(-50f, -50f), Vector2.zero, 34f,
            TextAlignmentOptions.Center, bodyFont);
        actions.gameObject.SetActive(false);

        RectTransform energy = CreateImage(
            overlayLayer,
            "EnergyAllocationPopup",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(650f, 450f),
            Vector2.zero,
            DarkPanel);
        CreateText(energy, "PlaceholderText",
            "ALLOCATE ENERGY\n\n−     1     +\n\nCONFIRM     CANCEL",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            new Vector2(-40f, -40f), Vector2.zero, 42f,
            TextAlignmentOptions.Center, titleFont);
        energy.gameObject.SetActive(false);
    }

    private static RectTransform CreateTabContent(
        RectTransform parent,
        string name,
        string placeholder)
    {
        RectTransform content = GetOrCreateRect(
            parent, name, Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), new Vector2(-40f, -40f), Vector2.zero);
        CreateText(content, "PlaceholderText", placeholder,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            new Vector2(-40f, -40f), Vector2.zero, 34f,
            TextAlignmentOptions.TopLeft, bodyFont);
        return content;
    }

    private static RectTransform CreateImage(
        RectTransform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 position,
        Color color)
    {
        RectTransform rect = GetOrCreateRect(
            parent, name, anchorMin, anchorMax, pivot, size, position);
        Image image = GetOrAddComponent<Image>(rect.gameObject);
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TMP_Text CreateText(
        RectTransform parent,
        string name,
        string value,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 position,
        float fontSize,
        TextAlignmentOptions alignment,
        TMP_FontAsset font,
        Color? color = null)
    {
        RectTransform rect = GetOrCreateRect(
            parent, name, anchorMin, anchorMax, pivot, size, position);
        TextMeshProUGUI text = GetOrAddComponent<TextMeshProUGUI>(rect.gameObject);
        Undo.RecordObject(text, "Repair Duel UI Text");

        ClearTemporaryFontMaterial(text);

        TMP_FontAsset resolvedFont = font != null
            ? font
            : TMP_Settings.defaultFontAsset;
        if (resolvedFont != null)
        {
            text.font = resolvedFont;
            text.fontSharedMaterial = resolvedFont.material;
        }

        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color ?? White;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.SetAllDirty();

        return text;
    }

    private static void ClearTemporaryFontMaterial(TextMeshProUGUI text)
    {
        SerializedObject serializedText = new SerializedObject(text);
        serializedText.Update();

        SerializedProperty fontMaterial =
            serializedText.FindProperty("m_fontMaterial");
        if (fontMaterial != null)
        {
            fontMaterial.objectReferenceValue = null;
        }

        SerializedProperty fontMaterials =
            serializedText.FindProperty("m_fontMaterials");
        if (fontMaterials != null)
        {
            fontMaterials.arraySize = 0;
        }

        serializedText.ApplyModifiedProperties();
    }

    private static Button CreateButton(
        RectTransform parent,
        string name,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 position,
        Color color,
        float fontSize)
    {
        RectTransform rect = GetOrCreateRect(
            parent, name, anchorMin, anchorMax, pivot, size, position);
        Image image = GetOrAddComponent<Image>(rect.gameObject);
        image.color = color;
        image.raycastTarget = true;
        Button button = GetOrAddComponent<Button>(rect.gameObject);
        button.targetGraphic = image;

        CreateText(
            rect,
            "Label",
            label,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            new Vector2(-18f, -12f),
            Vector2.zero,
            fontSize,
            TextAlignmentOptions.Center,
            titleFont);
        return button;
    }

    private static RectTransform GetOrCreateRect(
        RectTransform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 position)
    {
        RectTransform existing = FindDirectChild(parent, name);
        if (existing != null)
        {
            return existing;
        }

        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
        gameObject.layer = parent.gameObject.layer;
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
        createdObjectCount++;
        return rect;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject)
        where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = Undo.AddComponent<T>(gameObject);
        }

        return component;
    }

    private static RectTransform FindDirectChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == name)
            {
                return child as RectTransform;
            }
        }

        return null;
    }

    private static RectTransform FindInActiveScene(string name)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            RectTransform found = FindRecursive(roots[index].transform, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static RectTransform FindRecursive(Transform current, string name)
    {
        if (current.name == name)
        {
            return current as RectTransform;
        }

        for (int index = 0; index < current.childCount; index++)
        {
            RectTransform found = FindRecursive(current.GetChild(index), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private sealed class PhaseUiReferences
    {
        public Button MenuButton;
        public GameObject Choices;
        public Button BattleButton;
        public Button EndButton;
    }

    private sealed class SettingsUiReferences
    {
        public GameObject Overlay;
        public Button CloseButton;
        public Button DuelLogTabButton;
        public Button DeckInfoTabButton;
        public Button SettingsTabButton;
        public GameObject DuelLogContent;
        public GameObject DeckInfoContent;
        public GameObject SettingsContent;
        public Button SurrenderButton;
    }

    private sealed class ConfirmationUiReferences
    {
        public GameObject Popup;
        public TMP_Text MessageText;
        public Button AcceptButton;
        public Button CancelButton;
    }
}
