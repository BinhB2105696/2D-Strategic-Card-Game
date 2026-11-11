using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Displays a real GameSession using the sample scene's existing UI.
// The phase controls can submit actions; card interactions remain prototypes.
public sealed class DuelBoardReadOnlyDemo : MonoBehaviour
{
    private const float HandCardScale = 0.7f;

    [Header("Sample Match")]
    [SerializeField] private uint seed = 2026;
    [SerializeField] private CardData[] sampleCards;
    [SerializeField] private GameObject monsterCardPrefab;
    [SerializeField] private GameObject spellTrapCardPrefab;
    [SerializeField] private Sprite boardBackgroundSprite;
    [SerializeField] private Sprite playerOneAvatarSprite;
    [SerializeField] private Sprite playerTwoAvatarSprite;
    [SerializeField] private Sprite cardBackSprite;

    private readonly Dictionary<string, CardData> cardsById =
        new Dictionary<string, CardData>();
    private readonly List<GameObject> displayedCards = new List<GameObject>();
    private readonly Dictionary<long, RectTransform> renderedCardRects =
        new Dictionary<long, RectTransform>();
    private readonly HashSet<long> readyAttackers = new HashSet<long>();
    private GameSession session;
    private DuelCardInteractionController cardInteraction;
    private DuelEasyBotController easyBotController;
    private DuelPresentationController presentation;
    private DuelLogModel duelLog;
    private TMP_FontAsset runtimeFont;
    private bool hasRenderedMatch;
    private AppMatchRequest appMatch;
    public AppDuelFlow AppFlow { get; private set; }

    public GameSession Session => session;
    public Sprite NetworkCardBack => cardBackSprite;
    public TMP_FontAsset NetworkFont => runtimeFont;
    public void SetNetworkAvatar(string side, Sprite sprite)
    {
        Transform hud = transform.Find("SafeAreaRoot/HudLayer");
        if (hud != null && sprite != null) ApplyAvatar(hud, side, sprite);
    }

    public void InitializeNetworkAssets(IReadOnlyDictionary<string, CardData> catalog, Sprite avatar)
    {
        enabled = false;
        cardsById.Clear();
        foreach (var card in catalog) cardsById[card.Key] = card.Value;
        Transform hud = transform.Find("SafeAreaRoot/HudLayer");
        TMP_Text sample = hud != null ? hud.GetComponentInChildren<TMP_Text>(true) : null;
        runtimeFont = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
        Transform boardLayer = transform.Find("SafeAreaRoot/BoardLayer");
        Transform overlay = transform.Find("SafeAreaRoot/OverlayLayer");
        if (boardLayer != null && hud != null && overlay != null) ConfigureRuntimeSorting(boardLayer, hud, overlay);
        ApplyBoardBackground();
        if (hud != null)
        {
            ApplyAvatar(hud, "Player", avatar != null ? avatar : playerOneAvatarSprite);
            ApplyAvatar(hud, "Opp", playerTwoAvatarSprite);
        }
    }

    public GameObject CreateNetworkCard(CardData data, Transform parent, float scale, bool faceDown, bool defense)
    {
        if (data == null)
            foreach (CardData candidate in cardsById.Values) { data = candidate; break; }
        if (data == null || parent == null) return null;
        GameObject card = Instantiate(data.Category == CardCategory.Monster ? monsterCardPrefab : spellTrapCardPrefab, parent);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(250, 350);
        rect.localScale = Vector3.one * scale;
        rect.localRotation = Quaternion.Euler(0, 0, defense ? 90 : 0);
        CardView view = card.GetComponent<CardView>();
        view.SetCardData(data); view.SetFaceDown(faceDown);
        return card;
    }
    public bool IsPresenting => presentation != null && presentation.IsBusy;
    public bool IsEasyBotEnabled =>
        easyBotController != null && easyBotController.IsEnabled;
    public int ViewingPlayerIndex => IsEasyBotEnabled
        ? easyBotController.HumanPlayerIndex
        : session != null
            ? session.State.InteractionPlayerIndex
            : 0;

    public void Configure(
        CardData[] cards,
        GameObject monsterPrefab,
        GameObject spellTrapPrefab,
        Sprite boardBackground,
        Sprite playerOneAvatar,
        Sprite playerTwoAvatar,
        Sprite cardBack)
    {
        sampleCards = cards;
        monsterCardPrefab = monsterPrefab;
        spellTrapCardPrefab = spellTrapPrefab;
        boardBackgroundSprite = boardBackground;
        playerOneAvatarSprite = playerOneAvatar;
        playerTwoAvatarSprite = playerTwoAvatar;
        cardBackSprite = cardBack;
    }

    private void Start()
    {
        appMatch = AppMatchLaunch.Consume();
        if (!ValidateSetup())
            return;

        GameConfig config = GameConfig.CreateDefault();
        List<CardDefinition> deck = new List<CardDefinition>(config.MinimumDeckSize);

        foreach (CardData card in sampleCards)
            deck.Add(CardDataConverter.ToDefinition(card));

        try
        {
            cardInteraction = GetComponent<DuelCardInteractionController>();
            if (cardInteraction == null)
                cardInteraction = gameObject.AddComponent<DuelCardInteractionController>();
            cardInteraction.Initialize(this);

            List<CardDefinition> playerDeck = deck;
            if (appMatch != null)
            {
                if (!LocalDeckStore.Validate(appMatch.deck, appMatch.catalog, config, out string reason))
                    throw new InvalidOperationException(reason);
                playerDeck = new List<CardDefinition>();
                foreach (CardData card in appMatch.playerCards)
                    playerDeck.Add(CardDataConverter.ToDefinition(card));
                foreach (var item in appMatch.catalog) cardsById[item.Key] = item.Value;
                if (appMatch.avatar != null) playerOneAvatarSprite = appMatch.avatar;
            }
            session = GameSession.Create(config, appMatch != null ? appMatch.seed : seed, playerDeck, deck);
            duelLog = new DuelLogModel(session.State);

            presentation = GetComponent<DuelPresentationController>();
            if (presentation == null)
                presentation = gameObject.AddComponent<DuelPresentationController>();
            presentation.Initialize(this);

            easyBotController = GetComponent<DuelEasyBotController>();
            if (easyBotController == null)
                easyBotController = gameObject.AddComponent<DuelEasyBotController>();
            IBot selectedBot = appMatch != null && appMatch.difficulty == DuelBotDifficulty.Normal
                ? (IBot)new NormalBot() : new EasyBot();
            easyBotController.Initialize(this, selectedBot);

            DuelHudPrototypeController hudController =
                GetComponentInChildren<DuelHudPrototypeController>(true);
            if (hudController != null)
            {
                hudController.ConfigureEasyBot(easyBotController);
                hudController.ConfigureDuelLog(duelLog, this);
            }

            if (appMatch != null)
            {
                easyBotController.SetEnabled(true);
                GameObject view = Instantiate(appMatch.overlayPrefab);
                AppFlow = view.GetComponent<AppDuelFlow>();
                AppFlow.Initialize(this, appMatch, sampleCards);
                if (hudController != null) hudController.ConfigureAppExit(AppFlow);
            }

            // Show the opening hands before Recovery so its Draw and Energy
            // Refresh steps remain visible instead of being skipped on screen.
            Render(session.State);
            presentation.PlayOpeningDeal(session.State,
                () => ResolveAutomaticTurnStart());
            Debug.Log(
                "Read-only duel board ready: Turn " + session.State.TurnNumber +
                ", " + session.State.CurrentPhase +
                ", active player " + session.State.CurrentPlayerIndex +
                ", hands " + session.State.Players[0].Hand.Count + "/" +
                session.State.Players[1].Hand.Count +
                ", decks " + session.State.Players[0].Deck.Count + "/" +
                session.State.Players[1].Deck.Count + ".",
                this);
        }
        catch (Exception exception)
        {
            Debug.LogError("Cannot display sample duel: " + exception, this);
        }
    }

    public bool ResolveAutomaticTurnStart()
    {
        if (session == null)
            return false;
        if (presentation != null && presentation.IsPlaying)
            return false;

        // The match begins with a separate starting hand. Recovery's draw and
        // Energy Refresh are logged actions, then Standby advances to Main.
        // This same routine is used at the start of each later turn.
        while (session.State.CurrentPhase == TurnPhase.Recovery)
        {
            int player = session.State.CurrentPlayerIndex;
            GameAction action = session.State.CurrentRecoveryStep == RecoveryStep.Draw
                ? GameAction.ResolveDrawStep(player)
                : GameAction.ResolveEnergyRefreshStep(player);
            if (!ApplyAutomaticAction(action))
                return false;
        }

        if (session.State.CurrentPhase == TurnPhase.Standby &&
            !ApplyAutomaticAction(GameAction.AdvancePhase(
                session.State.CurrentPlayerIndex)))
            return false;

        bool reachedMain = session.State.CurrentPhase == TurnPhase.Main;
        if (reachedMain)
            PresentPendingOrRender();
        return reachedMain;
    }

    public void PresentTurnTransitionThenResolveAutomatic()
    {
        if (session == null)
            return;
        if (presentation != null && presentation.HasPendingEvents)
        {
            presentation.PlayPending(session.State,
                () => ResolveAutomaticTurnStart());
            return;
        }
        Render(session.State);
        ResolveAutomaticTurnStart();
    }

    public ActionResult TryApplyAction(GameAction action, bool refreshBoard = true)
    {
        if (session == null)
            return ActionResult.Failure("The sample duel is not ready.");
        if (presentation != null && presentation.IsPlaying)
            return ActionResult.Failure("Wait for the current animation to finish.");

        if (hasRenderedMatch)
            presentation?.CaptureBeforeAction(session.State);
        duelLog?.Capture(session.State);
        ActionResult result = session.TryApply(action);
        if (result.Succeeded)
        {
            duelLog?.Record(action, result.Events, session.State);
            if (hasRenderedMatch)
                presentation?.Record(result.Events);
            if (refreshBoard)
                PresentPendingOrRender();
        }
        else
        {
            presentation?.DiscardEmptyCapture();
        }
        return result;
    }

    public bool IsAutomatedPlayer(int playerIndex)
    {
        return easyBotController != null &&
            easyBotController.ControlsPlayer(playerIndex);
    }

    public void CloseCardWindows()
    {
        cardInteraction?.CloseAllCardWindows();
    }

    private bool ApplyAutomaticAction(GameAction action)
    {
        if (hasRenderedMatch)
            presentation?.CaptureBeforeAction(session.State);
        duelLog?.Capture(session.State);
        ActionResult result = session.TryApply(action);
        if (result.Succeeded)
        {
            duelLog?.Record(action, result.Events, session.State);
            if (hasRenderedMatch)
                presentation?.Record(result.Events);
            return true;
        }

        presentation?.DiscardEmptyCapture();
        Debug.LogError("Automatic turn step failed: " + result.ErrorMessage, this);
        return false;
    }

    [ContextMenu("Refresh Read-Only Board")]
    public void RefreshBoard()
    {
        if (session != null)
            PresentPendingOrRender();
    }

    private void PresentPendingOrRender()
    {
        if (presentation != null && presentation.IsPlaying)
            return;
        if (presentation != null && presentation.HasPendingEvents)
        {
            presentation.PlayPending(session.State);
            return;
        }
        presentation?.DiscardEmptyCapture();
        Render(session.State);
    }

    public RectTransform FindRenderedCard(long instanceId)
    {
        return renderedCardRects.TryGetValue(instanceId, out RectTransform rect)
            ? rect
            : null;
    }

    public void Render(GameState state)
    {
        if (state == null)
            return;

        Transform safeArea = transform.Find("SafeAreaRoot");
        if (safeArea == null)
        {
            Debug.LogError("SafeAreaRoot is missing from Field_Canvas.", this);
            return;
        }

        ClearDisplayedCards();
        Transform board = safeArea.Find("BoardLayer");
        Transform hud = safeArea.Find("HudLayer");
        Transform overlay = safeArea.Find("OverlayLayer");
        if (board == null || hud == null || overlay == null)
        {
            Debug.LogError("The existing Board, HUD or Overlay layer is missing.", this);
            return;
        }

        ConfigureRuntimeSorting(board, hud, overlay);
        ApplyBoardBackground();
        if (runtimeFont == null)
        {
            TMP_Text sampleText = hud.GetComponentInChildren<TMP_Text>(true);
            runtimeFont = sampleText != null
                ? sampleText.font
                : TMP_Settings.defaultFontAsset;
        }

        cardInteraction?.BeginRender(state);

        readyAttackers.Clear();
        if (!state.IsFinished && state.CurrentPhase == TurnPhase.Battle &&
            !state.HasOpenResponseWindow && !state.HasPendingTriggeredEffect)
        {
            IReadOnlyList<GameAction> legalActions = GameRules.GetLegalActions(
                state,
                state.InteractionPlayerIndex);
            foreach (GameAction legalAction in legalActions)
                if (legalAction.Type == GameActionType.Attack)
                    readyAttackers.Add(legalAction.SourceInstanceId);
        }

        int viewingPlayerIndex = IsEasyBotEnabled
            ? easyBotController.HumanPlayerIndex
            : state.InteractionPlayerIndex;
        bool humanMayInteract =
            !IsAutomatedPlayer(state.InteractionPlayerIndex);
        PlayerState activePlayer = state.Players[viewingPlayerIndex];
        PlayerState opponent = state.Players[1 - viewingPlayerIndex];
        RenderPlayer(
            activePlayer,
            board,
            hud,
            "Player",
            true,
            humanMayInteract);
        RenderPlayer(
            opponent,
            board,
            hud,
            "Opp",
            false,
            humanMayInteract);

        string phase = state.CurrentPhase + " Phase";
        SetText(hud, "TurnPhaseHUD/TurnText", "Turn " + state.TurnNumber);
        SetText(hud, "TurnPhaseHUD/PhaseText", phase);
        SetText(overlay, "SettingsOverlay/TurnPhaseText",
            "Turn " + state.TurnNumber + " - " + phase);
        SetText(overlay, "SettingsOverlay/PlayerHpText",
            "LP: " + activePlayer.Health);
        SetText(overlay, "SettingsOverlay/OpponentHpText",
            "LP: " + opponent.Health);

        DuelHudPrototypeController hudController =
            GetComponentInChildren<DuelHudPrototypeController>(true);
        if (hudController != null)
            hudController.RefreshPhaseControls(state);

        if (state.IsFinished)
            cardInteraction?.ShowMatchResult(state);
        else if (state.HasOpenResponseWindow && humanMayInteract)
            cardInteraction?.ShowResponsePrompt();
        else if (state.HasPendingTriggeredEffect && humanMayInteract)
            cardInteraction?.ShowTriggeredEffectPrompt();
        hasRenderedMatch = true;
    }

    private void RenderPlayer(
        PlayerState player,
        Transform board,
        Transform hud,
        string side,
        bool showHand,
        bool allowAnyInteraction)
    {
        SetText(hud, side + "_HUD/LpText", "LP:" + player.Health);
        SetText(hud, side + "_EnergyWidget/ValueText",
            player.AvailableEnergy.ToString());
        SetText(board, side + "_PileColumn/" + side + "_DeckZone/CountText",
            player.Deck.Count.ToString());
        SetText(board, side + "_PileColumn/" + side + "_GraveyardZone/CountText",
            player.Graveyard.Count.ToString());
        ApplyAvatar(hud, side, GetAvatar(player.PlayerIndex));
        if (appMatch != null)
        {
            Transform name = FindPath(hud, side + "_HUD/NameText");
            TMP_Text label = name != null ? name.GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                label.richText = false;
                label.text = player.PlayerIndex == 0 ? appMatch.profile.guestName : "Easy Bot";
            }
        }
        RenderDeckAndGraveyard(player, board, side);

        RenderHand(
            player.Hand,
            board,
            side,
            showHand,
            allowAnyInteraction);
        RenderZones(player.MonsterZones, board, side + "_MonsterRow",
            side + "_MonsterZone_", true, showHand, !showHand,
            allowAnyInteraction);
        RenderZones(player.SpellTrapZones, board, side + "_SpellTrapRow",
            side + "_SpellTrapZone_", false, showHand, !showHand,
            allowAnyInteraction);
        RenderFieldSpell(
            player.FieldSpell,
            board,
            side,
            allowAnyInteraction);
    }

    private void ApplyBoardBackground()
    {
        // Battlefield_Background is a sibling of SafeAreaRoot under the
        // Field_Canvas, so it must be resolved from this component's root.
        Transform target = transform.Find("Battlefield_Background");
        Image image = target != null ? target.GetComponent<Image>() : null;
        if (image == null || boardBackgroundSprite == null)
            return;

        image.sprite = boardBackgroundSprite;
        image.color = Color.white;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }

    private Sprite GetAvatar(int playerIndex)
    {
        return playerIndex == 0
            ? playerOneAvatarSprite
            : playerTwoAvatarSprite;
    }

    private static void ApplyAvatar(
        Transform hud,
        string side,
        Sprite avatar)
    {
        Transform frame = hud.Find(side + "_HUD/AvatarFrame");
        Image image = frame != null ? frame.GetComponent<Image>() : null;
        if (image == null || avatar == null)
            return;

        image.sprite = avatar;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        Transform placeholder = hud.Find(
            side + "_HUD/AvatarPlaceholderText");
        if (placeholder != null)
            placeholder.gameObject.SetActive(false);
    }

    private void RenderDeckAndGraveyard(
        PlayerState player,
        Transform board,
        string side)
    {
        Transform pileColumn = board.Find(side + "_PileColumn");
        if (pileColumn == null)
            return;

        Transform deckZone = pileColumn.Find(side + "_DeckZone");
        Transform deckAnchor = deckZone != null
            ? deckZone.Find("CardAnchor")
            : null;
        if (player.Deck.Count > 0 && deckAnchor != null &&
            cardBackSprite != null)
        {
            CreateDeckBack(deckAnchor);
        }

        Transform graveyardZone = pileColumn.Find(
            side + "_GraveyardZone");
        Transform graveyardAnchor = graveyardZone != null
            ? graveyardZone.Find("CardAnchor")
            : null;
        if (player.Graveyard.Count > 0 && graveyardAnchor != null)
        {
            CardInstance latest =
                player.Graveyard[player.Graveyard.Count - 1];
            CreateCard(
                latest,
                graveyardAnchor,
                1f,
                false,
                false,
                false);
        }
        if (graveyardZone != null)
            CreateGraveyardTapTarget(graveyardZone, player.PlayerIndex);
    }

    private void CreateGraveyardTapTarget(Transform zone, int playerIndex)
    {
        GameObject target = new GameObject(
            "RuntimeGraveyardTapTarget", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button));
        target.layer = zone.gameObject.layer;
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.SetParent(zone, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = target.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
        Button button = target.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
            cardInteraction?.ShowGraveyard(playerIndex));
        displayedCards.Add(target);
    }

    private void CreateDeckBack(Transform parent)
    {
        GameObject backObject = new GameObject(
            "RuntimeDeckCardBack",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        backObject.layer = parent.gameObject.layer;
        backObject.transform.SetParent(parent, false);
        displayedCards.Add(backObject);

        RectTransform rect = backObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot =
            new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(250f, 350f);

        Image image = backObject.GetComponent<Image>();
        image.sprite = cardBackSprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void RenderHand(
        IReadOnlyList<CardInstance> hand,
        Transform board,
        string side,
        bool showHand,
        bool allowAnyInteraction)
    {
        Transform content = board.Find(side + "_HandArea/HandContent");
        Transform template = content != null
            ? content.Find("HandCardSlot_Template")
            : null;
        if (template == null)
        {
            Debug.LogError(side + " hand template is missing.", this);
            return;
        }

        DuelHandLayoutController handLayout =
            content.GetComponent<DuelHandLayoutController>();
        if (handLayout == null)
            handLayout = content.gameObject.AddComponent<DuelHandLayoutController>();
        handLayout.BeginLayout(showHand, !showHand);

        for (int i = 0; i < hand.Count; i++)
        {
            GameObject slot = Instantiate(template.gameObject, content);
            slot.name = "HandCardSlot_" + hand[i].InstanceId;
            displayedCards.Add(slot);
            Transform placeholder = slot.transform.Find("PlaceholderText");
            if (placeholder != null)
                placeholder.gameObject.SetActive(false);

            // The saved template is 175x245, exactly 70% of a 250x350 card.
            CreateCard(
                hand[i],
                slot.transform,
                HandCardScale,
                false,
                !showHand,
                allowAnyInteraction && showHand);
            handLayout.AddSlot(slot.GetComponent<RectTransform>());
            slot.SetActive(true);
        }

        handLayout.ApplyLayout();
    }

    private void RenderZones(
        IReadOnlyList<CardInstance> cards,
        Transform board,
        string rowName,
        string zonePrefix,
        bool monsterRow,
        bool ownerCanInteract,
        bool mirrorZoneOrder,
        bool allowAnyInteraction)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            int displayZoneIndex = mirrorZoneOrder
                ? cards.Count - 1 - i
                : i;
            Transform zone = board.Find(
                rowName + "/" + zonePrefix + displayZoneIndex);
            if (zone == null)
            {
                Debug.LogError(
                    "Missing zone " + zonePrefix + displayZoneIndex,
                    this);
                continue;
            }

            if (monsterRow)
            {
                DuelMonsterZoneStatusView status =
                    zone.GetComponent<DuelMonsterZoneStatusView>();
                if (status == null)
                    status = zone.gameObject.AddComponent<DuelMonsterZoneStatusView>();
                status.Initialize(runtimeFont);
                status.SetMonster(cards[i]);
            }

            if (cards[i] == null)
                continue;

            Transform anchor = zone.Find("CardAnchor");
            if (anchor == null)
            {
                Debug.LogError("Missing CardAnchor in " + zonePrefix + i, this);
                continue;
            }

            bool defense = monsterRow && cards[i].BattlePosition == BattlePosition.Defense;
            // Existing CardAnchor is already scaled to 60% by the scene.
            CreateCard(
                cards[i],
                anchor,
                1f,
                defense,
                cards[i].IsFaceDown,
                allowAnyInteraction &&
                (ownerCanInteract || !cards[i].IsFaceDown));
            if (monsterRow && readyAttackers.Contains(cards[i].InstanceId))
                displayedCards.Add(DuelAttackReadyGlow.Create(anchor));
        }
    }

    private void RenderFieldSpell(
        CardInstance fieldSpell,
        Transform board,
        string side,
        bool allowAnyInteraction)
    {
        if (fieldSpell == null)
            return;

        Transform zone = board.Find(side + "_FieldZone");
        Transform anchor = zone != null ? zone.Find("CardAnchor") : null;
        if (anchor == null)
        {
            Debug.LogError(side + " Field Zone/CardAnchor is missing.", this);
            return;
        }

        CreateCard(
            fieldSpell,
            anchor,
            1f,
            false,
            false,
            allowAnyInteraction);
    }

    private void CreateCard(
        CardInstance instance,
        Transform parent,
        float scale,
        bool defense,
        bool faceDown,
        bool allowInteraction)
    {
        if (!cardsById.TryGetValue(instance.Definition.CardId, out CardData cardData))
        {
            Debug.LogWarning("No visual CardData for " + instance.Definition.CardId, this);
            return;
        }

        GameObject prefab = cardData.Category == CardCategory.Monster
            ? monsterCardPrefab
            : spellTrapCardPrefab;
        GameObject card = Instantiate(prefab, parent);
        card.name = "Card_" + instance.InstanceId;
        displayedCards.Add(card);

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(250f, 350f);
        rect.localScale = Vector3.one * scale;
        rect.localRotation = Quaternion.Euler(0f, 0f, defense ? 90f : 0f);
        renderedCardRects[instance.InstanceId] = rect;

        CardView view = card.GetComponent<CardView>();
        view.SetCardData(cardData);
        view.SetFaceDown(faceDown);
        cardInteraction?.BindCard(instance, card, allowInteraction);
    }

    public GameObject CreateRuntimeCardPreview(
        CardInstance instance,
        Transform parent)
    {
        if (instance == null || parent == null ||
            !cardsById.TryGetValue(
                instance.Definition.CardId,
                out CardData cardData))
        {
            return null;
        }

        GameObject prefab = cardData.Category == CardCategory.Monster
            ? monsterCardPrefab
            : spellTrapCardPrefab;
        GameObject card = Instantiate(prefab, parent);
        CardView view = card.GetComponent<CardView>();
        if (view != null)
        {
            view.SetCardData(cardData);
            view.SetFaceDown(false);
        }
        return card;
    }

    private static void ConfigureRuntimeSorting(
        Transform board,
        Transform hud,
        Transform overlay)
    {
        EnsureOverrideCanvas(board.Find("Player_HandArea"), 10, true);
        EnsureOverrideCanvas(board.Find("Opp_HandArea"), 10, true);
        EnsureOverrideCanvas(hud.Find("TurnPhaseHUD"), 20, true);
        EnsureOverrideCanvas(hud.Find("SettingsButton"), 20, true);
        EnsureOverrideCanvas(hud.Find("PhaseControl"), 20, true);
        EnsureOverrideCanvas(overlay, 30, true);
        EnsureOverrideCanvas(overlay.Find("SettingsOverlay"), 50, true);
        EnsureOverrideCanvas(
            overlay.Find("PhaseConfirmationPopup"),
            60,
            true);
    }

    private static void EnsureOverrideCanvas(
        Transform target,
        int sortingOrder,
        bool needsRaycaster)
    {
        if (target == null)
            return;

        Canvas canvas = target.GetComponent<Canvas>();
        if (canvas == null)
            canvas = target.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        if (needsRaycaster && target.GetComponent<GraphicRaycaster>() == null)
            target.gameObject.AddComponent<GraphicRaycaster>();
    }

    private bool ValidateSetup()
    {
        GameConfig config = GameConfig.CreateDefault();
        if (sampleCards == null || sampleCards.Length == 0 ||
            monsterCardPrefab == null || spellTrapCardPrefab == null)
        {
            Debug.LogError("Sample cards and both card prefabs are required.", this);
            return false;
        }
        if (sampleCards.Length < config.MinimumDeckSize ||
            sampleCards.Length > config.MaximumDeckSize)
        {
            Debug.LogError(
                "Demo deck must contain " + config.MinimumDeckSize + "-" +
                config.MaximumDeckSize + " cards.",
                this);
            return false;
        }

        cardsById.Clear();
        Dictionary<string, int> copyCounts = new Dictionary<string, int>();
        foreach (CardData card in sampleCards)
        {
            if (card == null || string.IsNullOrEmpty(card.CardId))
            {
                Debug.LogError(
                    "Demo deck cards must have nonempty Card IDs.",
                    this);
                return false;
            }
            int count = copyCounts.TryGetValue(card.CardId, out int existing)
                ? existing + 1
                : 1;
            copyCounts[card.CardId] = count;
            if (count > 3)
            {
                Debug.LogError(
                    "Demo deck cannot contain more than 3 copies of " +
                    card.CardName + ".",
                    this);
                return false;
            }
            if (card.Effects.Count == 0)
            {
                Debug.LogError(
                    "Demo deck card has no executable effect: " +
                    card.CardName + ".",
                    this);
                return false;
            }
            if (!cardsById.ContainsKey(card.CardId))
                cardsById.Add(card.CardId, card);
        }
        return true;
    }

    private void ClearDisplayedCards()
    {
        foreach (GameObject card in displayedCards)
            if (card != null)
                Destroy(card);
        displayedCards.Clear();
        renderedCardRects.Clear();
    }

    private void SetText(Transform root, string path, string value)
    {
        Transform target = FindPath(root, path);
        TMP_Text label = target != null ? target.GetComponent<TMP_Text>() : null;
        if (label != null)
            label.text = value;
        else
            Debug.LogWarning("Read-only UI label missing: " + path, this);
    }

    private static Transform FindPath(Transform root, string path)
    {
        Transform current = root;
        foreach (string part in path.Split('/'))
        {
            Transform next = current.Find(part);
            if (next == null)
                next = FindDescendant(current, part);
            if (next == null)
                return null;
            current = next;
        }
        return current;
    }

    private static Transform FindDescendant(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            Transform nested = FindDescendant(child, name);
            if (nested != null)
                return nested;
        }
        return null;
    }
}
