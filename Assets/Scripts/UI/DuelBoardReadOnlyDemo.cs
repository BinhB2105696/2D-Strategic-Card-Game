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

    private readonly Dictionary<string, CardData> cardsById =
        new Dictionary<string, CardData>();
    private readonly List<GameObject> displayedCards = new List<GameObject>();
    private GameSession session;
    private DuelCardInteractionController cardInteraction;
    private TMP_FontAsset runtimeFont;

    public GameSession Session => session;

    public void Configure(CardData[] cards, GameObject monsterPrefab, GameObject spellTrapPrefab)
    {
        sampleCards = cards;
        monsterCardPrefab = monsterPrefab;
        spellTrapCardPrefab = spellTrapPrefab;
    }

    private void Start()
    {
        if (!ValidateSetup())
            return;

        GameConfig config = GameConfig.CreateDefault();
        List<CardDefinition> deck = new List<CardDefinition>(config.MinimumDeckSize);

        for (int i = 0; i < config.MinimumDeckSize; i++)
            deck.Add(CardDataConverter.ToDefinition(sampleCards[i % sampleCards.Length]));

        try
        {
            cardInteraction = GetComponent<DuelCardInteractionController>();
            if (cardInteraction == null)
                cardInteraction = gameObject.AddComponent<DuelCardInteractionController>();
            cardInteraction.Initialize(this);

            session = GameSession.Create(config, seed, deck, deck);
            if (!ResolveAutomaticTurnStart())
                return;
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
            Render(session.State);
        return reachedMain;
    }

    public ActionResult TryApplyAction(GameAction action, bool refreshBoard = true)
    {
        if (session == null)
            return ActionResult.Failure("The sample duel is not ready.");

        ActionResult result = session.TryApply(action);
        if (result.Succeeded && refreshBoard)
            Render(session.State);
        return result;
    }

    private bool ApplyAutomaticAction(GameAction action)
    {
        ActionResult result = session.TryApply(action);
        if (result.Succeeded)
            return true;

        Debug.LogError("Automatic turn step failed: " + result.ErrorMessage, this);
        return false;
    }

    [ContextMenu("Refresh Read-Only Board")]
    public void RefreshBoard()
    {
        if (session != null)
            Render(session.State);
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
        if (runtimeFont == null)
        {
            TMP_Text sampleText = hud.GetComponentInChildren<TMP_Text>(true);
            runtimeFont = sampleText != null
                ? sampleText.font
                : TMP_Settings.defaultFontAsset;
        }

        cardInteraction?.BeginRender(state);

        PlayerState activePlayer = state.Players[state.InteractionPlayerIndex];
        PlayerState opponent = state.Players[1 - state.InteractionPlayerIndex];
        RenderPlayer(activePlayer, board, hud, "Player", true);
        RenderPlayer(opponent, board, hud, "Opp", false);

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
        else if (state.HasOpenResponseWindow)
            cardInteraction?.ShowResponsePrompt();
        else if (state.HasPendingTriggeredEffect)
            cardInteraction?.ShowTriggeredEffectPrompt();
    }

    private void RenderPlayer(
        PlayerState player,
        Transform board,
        Transform hud,
        string side,
        bool isLocalPlayer)
    {
        SetText(hud, side + "_HUD/LpText", "LP:" + player.Health);
        SetText(hud, side + "_EnergyWidget/ValueText",
            player.AvailableEnergy.ToString());
        SetText(board, side + "_PileColumn/" + side + "_DeckZone/CountText",
            player.Deck.Count.ToString());
        SetText(board, side + "_PileColumn/" + side + "_GraveyardZone/CountText",
            player.Graveyard.Count.ToString());

        RenderHand(player.Hand, board, side, isLocalPlayer);
        RenderZones(player.MonsterZones, board, side + "_MonsterRow",
            side + "_MonsterZone_", true, isLocalPlayer, !isLocalPlayer);
        RenderZones(player.SpellTrapZones, board, side + "_SpellTrapRow",
            side + "_SpellTrapZone_", false, isLocalPlayer, !isLocalPlayer);
        RenderFieldSpell(player.FieldSpell, board, side, isLocalPlayer);
    }

    private void RenderHand(
        IReadOnlyList<CardInstance> hand,
        Transform board,
        string side,
        bool isLocalPlayer)
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
        handLayout.BeginLayout(isLocalPlayer);

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
                !isLocalPlayer,
                isLocalPlayer);
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
        bool mirrorZoneOrder)
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
                ownerCanInteract || !cards[i].IsFaceDown);
        }
    }

    private void RenderFieldSpell(
        CardInstance fieldSpell,
        Transform board,
        string side,
        bool ownerCanInteract)
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
            true);
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
