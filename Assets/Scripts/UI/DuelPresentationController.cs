using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Plays accepted GameEvents over the previously rendered board. The core has
// already resolved each action; these objects never change gameplay state.
public sealed class DuelPresentationController : MonoBehaviour
{
    private sealed class CardBefore
    {
        public CardInstance Instance;
        public CardZone Zone;
        public int Owner;
        public int Index;
        public bool FaceDown;
        public int Health;
        public int MaximumHealth;
        public int Energy;
        public RectTransform Visual;
    }

    private sealed class BoardBefore
    {
        public int ViewingPlayer;
        public int TurnNumber;
        public TurnPhase Phase;
        public RecoveryStep RecoveryStep;
        public readonly int[] Health = new int[2];
        public readonly int[] Energy = new int[2];
        public readonly Dictionary<long, CardBefore> Cards =
            new Dictionary<long, CardBefore>();
    }

    [Header("Runtime presentation timing")]
    [SerializeField, Range(0.1f, 1.2f)] private float cardMoveSeconds = 0.38f;
    [SerializeField, Range(0.1f, 0.8f)] private float fastDrawSeconds = 0.2f;
    [SerializeField, Range(0.1f, 0.8f)] private float recoveryDrawSeconds = 0.32f;
    [SerializeField, Range(0.3f, 1f)] private float recoveryStepSeconds = 0.65f;
    [SerializeField, Range(0.08f, 0.8f)] private float cardFlipSeconds = 0.24f;
    [SerializeField, Range(0.08f, 0.8f)] private float attackSeconds = 0.32f;
    [SerializeField, Range(0.08f, 1f)] private float numberSeconds = 0.42f;
    [SerializeField, Range(0.08f, 1f)] private float bannerSeconds = 0.38f;
    [SerializeField] private AnimationCurve motionCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private readonly List<GameEvent> pendingEvents = new List<GameEvent>();
    private readonly Dictionary<long, RectTransform> ghosts =
        new Dictionary<long, RectTransform>();
    private readonly Dictionary<long, int> visibleMonsterHealth =
        new Dictionary<long, int>();
    private readonly Dictionary<long, int> visibleMonsterEnergy =
        new Dictionary<long, int>();
    private readonly List<GameObject> hiddenGraveStatuses =
        new List<GameObject>();
    private readonly Dictionary<long, int> visibleMonsterMaximumHealth = new Dictionary<long, int>();
    private readonly int[] visibleHealth = new int[2];
    private readonly int[] visibleEnergy = new int[2];
    private DuelBoardReadOnlyDemo board;
    private BoardBefore before;
    private RectTransform visualRoot;
    private RectTransform cardLayer;
    private GameObject blocker;
    private TMP_FontAsset font;
    private long pendingAttackerId;
    private long pendingAttackTargetId;
    private bool pendingAttackLunged;
    private float recoveryStepStartedAt;
    private int displayedTurnNumber;
    private float phaseHudBaseFontSize;
    private GameObject movingStatus;
    private GameObject hiddenStatus;

    public bool IsPlaying { get; private set; }
    public bool HasPendingEvents => pendingEvents.Count > 0;
    public bool IsBusy => IsPlaying || HasPendingEvents;

    public void Initialize(DuelBoardReadOnlyDemo duelBoard)
    {
        board = duelBoard;
        Transform layer = transform.Find("SafeAreaRoot/OverlayLayer");
        if (layer == null)
        {
            Debug.LogError("OverlayLayer is required for duel presentation.", this);
            return;
        }

        GameObject root = new GameObject(
            "RuntimePresentationLayer",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster));
        root.layer = layer.gameObject.layer;
        visualRoot = root.GetComponent<RectTransform>();
        visualRoot.SetParent(layer, false);
        visualRoot.anchorMin = Vector2.zero;
        visualRoot.anchorMax = Vector2.one;
        visualRoot.offsetMin = Vector2.zero;
        visualRoot.offsetMax = Vector2.zero;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 18;

        GameObject movingCards = new GameObject(
            "RuntimeMovingCards", typeof(RectTransform), typeof(Canvas));
        movingCards.layer = root.layer;
        cardLayer = movingCards.GetComponent<RectTransform>();
        cardLayer.SetParent(visualRoot, false);
        cardLayer.anchorMin = Vector2.zero;
        cardLayer.anchorMax = Vector2.one;
        cardLayer.offsetMin = Vector2.zero;
        cardLayer.offsetMax = Vector2.zero;
        Canvas cardCanvas = movingCards.GetComponent<Canvas>();
        cardCanvas.overrideSorting = true;
        cardCanvas.sortingOrder = 12;

        blocker = new GameObject(
            "PresentationInputBlocker",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        blocker.layer = root.layer;
        RectTransform blockerRect = blocker.GetComponent<RectTransform>();
        blockerRect.SetParent(visualRoot, false);
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;
        Image blockerImage = blocker.GetComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0f);
        blockerImage.raycastTarget = true;
        Canvas blockerCanvas = blocker.AddComponent<Canvas>();
        blockerCanvas.overrideSorting = true;
        blockerCanvas.sortingOrder = 21;
        blocker.AddComponent<GraphicRaycaster>();
        blocker.SetActive(false);
        TMP_Text phaseText = transform.Find(
            "SafeAreaRoot/HudLayer/TurnPhaseHUD/PhaseText")
            ?.GetComponent<TMP_Text>();
        phaseHudBaseFontSize = phaseText != null ? phaseText.fontSize : 30f;
    }

    public void CaptureBeforeAction(GameState state)
    {
        if (before != null || visualRoot == null || state == null)
            return;

        Canvas.ForceUpdateCanvases();
        before = new BoardBefore
        {
            ViewingPlayer = board.ViewingPlayerIndex,
            TurnNumber = state.TurnNumber,
            Phase = state.CurrentPhase,
            RecoveryStep = state.CurrentRecoveryStep
        };
        for (int playerIndex = 0; playerIndex < state.Players.Count; playerIndex++)
        {
            PlayerState player = state.Players[playerIndex];
            before.Health[playerIndex] = player.Health;
            before.Energy[playerIndex] = player.AvailableEnergy;
            AddCards(player.Deck, CardZone.Deck, playerIndex);
            AddCards(player.Hand, CardZone.Hand, playerIndex);
            AddCards(player.Graveyard, CardZone.Graveyard, playerIndex);
            AddCards(player.MonsterZones, CardZone.MonsterZone, playerIndex);
            AddCards(player.SpellTrapZones, CardZone.SpellTrapZone, playerIndex);
            AddCard(player.FieldSpell, CardZone.FieldZone, playerIndex, 0);
        }
    }

    public void Record(IReadOnlyList<GameEvent> events)
    {
        if (before == null || events == null)
            return;
        for (int index = 0; index < events.Count; index++)
            pendingEvents.Add(events[index]);
    }

    public void DiscardEmptyCapture()
    {
        if (pendingEvents.Count == 0 && !IsPlaying)
            before = null;
    }

    public void PlayPending(GameState finalState, Action onComplete = null)
    {
        if (IsPlaying || before == null || pendingEvents.Count == 0 ||
            visualRoot == null)
            return;

        List<GameEvent> events = new List<GameEvent>(pendingEvents);
        pendingEvents.Clear();
        IsPlaying = true;
        blocker.SetActive(true);
        board.CloseCardWindows();
        visibleHealth[0] = before.Health[0];
        visibleHealth[1] = before.Health[1];
        visibleEnergy[0] = before.Energy[0];
        visibleEnergy[1] = before.Energy[1];
        displayedTurnNumber = before.TurnNumber;
        visibleMonsterHealth.Clear();
        visibleMonsterMaximumHealth.Clear();
        visibleMonsterEnergy.Clear();
        foreach (KeyValuePair<long, CardBefore> pair in before.Cards)
        {
            visibleMonsterHealth[pair.Key] = pair.Value.Health;
            visibleMonsterMaximumHealth[pair.Key] = pair.Value.MaximumHealth;
            visibleMonsterEnergy[pair.Key] = pair.Value.Energy;
        }
        StartCoroutine(PlaySequence(events, finalState, onComplete));
    }

    public void PlayOpeningDeal(GameState state, Action onComplete = null)
    {
        if (IsBusy || before != null || visualRoot == null || state == null)
            return;

        CaptureBeforeAction(state);
        List<CardInstance> dealtCards = new List<CardInstance>();
        int firstPlayer = before.ViewingPlayer;
        int maxCards = Mathf.Max(
            state.Players[0].Hand.Count,
            state.Players[1].Hand.Count);
        for (int cardIndex = 0; cardIndex < maxCards; cardIndex++)
            for (int side = 0; side < 2; side++)
            {
                int playerIndex = side == 0 ? firstPlayer : 1 - firstPlayer;
                IReadOnlyList<CardInstance> hand =
                    state.Players[playerIndex].Hand;
                if (cardIndex >= hand.Count)
                    continue;
                CardInstance card = hand[cardIndex];
                RectTransform ghost = GetGhost(card.InstanceId);
                if (ghost == null)
                    continue;
                ghost.anchoredPosition = ZonePosition(
                    playerIndex, CardZone.Deck, 0);
                ghost.localScale = Vector3.one * 0.6f;
                ghost.GetComponent<CardView>()?.SetFaceDown(true);
                dealtCards.Add(card);
            }

        if (dealtCards.Count == 0)
        {
            before = null;
            onComplete?.Invoke();
            return;
        }
        IsPlaying = true;
        blocker.SetActive(true);
        StartCoroutine(PlayOpeningDealSequence(state, onComplete));
    }

    private void AddCards(
        IReadOnlyList<CardInstance> cards,
        CardZone zone,
        int playerIndex)
    {
        for (int index = 0; index < cards.Count; index++)
            AddCard(cards[index], zone, playerIndex, index);
    }

    private void AddCard(
        CardInstance card,
        CardZone zone,
        int playerIndex,
        int index)
    {
        if (card == null)
            return;
        before.Cards[card.InstanceId] = new CardBefore
        {
            Instance = card,
            Zone = zone,
            Owner = playerIndex,
            Index = index,
            FaceDown = card.IsFaceDown ||
                (zone == CardZone.Hand && playerIndex != before.ViewingPlayer),
            Health = card.CurrentHealth,
            MaximumHealth = card.MaximumHealth,
            Energy = card.AllocatedEnergy,
            Visual = board.FindRenderedCard(card.InstanceId)
        };
    }

    private IEnumerator PlaySequence(
        IReadOnlyList<GameEvent> events,
        GameState finalState,
        Action onComplete)
    {
        if (before.Phase == TurnPhase.Recovery)
            SetRecoveryHud(before.RecoveryStep);
        for (int index = 0; index < events.Count; index++)
        {
            if (IsDrawEvent(events[index]))
            {
                List<GameEvent> draws = new List<GameEvent>();
                while (index < events.Count && IsDrawEvent(events[index]))
                    draws.Add(events[index++]);
                index--;
                float duration = draws[0].Type == GameEventType.CardDrawn &&
                    draws[0].Phase == TurnPhase.Recovery
                    ? recoveryDrawSeconds : fastDrawSeconds;
                yield return PlayDraws(draws, duration);
                continue;
            }

            if (events[index].Type == GameEventType.CardPlacedInSpellTrapZone)
            {
                GameEvent placed = events[index];
                bool faceDown = index + 1 < events.Count &&
                    events[index + 1].Type == GameEventType.CardSet &&
                    events[index + 1].CardInstanceId == placed.CardInstanceId;
                yield return MoveCardTo(placed.CardInstanceId,
                    CardZone.SpellTrapZone, placed.PlayerIndex,
                    placed.Amount, faceDown);
                continue;
            }

            yield return PlayEvent(events[index], finalState);
        }

        ClearGhosts();
        board.Render(finalState);
        hiddenGraveStatuses.Clear();
        before = null;
        blocker.SetActive(false);
        IsPlaying = false;
        if (finalState.ActiveChain == null)
        {
            pendingAttackerId = 0;
            pendingAttackTargetId = 0;
            pendingAttackLunged = false;
        }
        onComplete?.Invoke();
    }

    private IEnumerator PlayOpeningDealSequence(
        GameState state, Action onComplete)
    {
        int maxCards = Mathf.Max(state.Players[0].Hand.Count,
            state.Players[1].Hand.Count);
        for (int cardIndex = 0; cardIndex < maxCards; cardIndex++)
        {
            int running = 0;
            for (int side = 0; side < 2; side++)
            {
                IReadOnlyList<CardInstance> hand = state.Players[side].Hand;
                if (cardIndex >= hand.Count)
                    continue;
                CardInstance card = hand[cardIndex];
                running++;
                StartCoroutine(MoveDrawAndSignal(card.InstanceId,
                    card.OwnerPlayerIndex, fastDrawSeconds,
                    () => running--));
            }
            while (running > 0)
                yield return null;
        }

        ClearGhosts();
        board.Render(state);
        before = null;
        blocker.SetActive(false);
        IsPlaying = false;
        onComplete?.Invoke();
    }

    private static bool IsDrawEvent(GameEvent eventData)
    {
        return eventData.Type == GameEventType.CardDrawn ||
            eventData.Type == GameEventType.CardAddedToHand;
    }

    private IEnumerator PlayDraws(IReadOnlyList<GameEvent> draws, float duration)
    {
        // Each player's cards stay in draw order, while the two hands animate
        // concurrently whenever one action yields draws for both players.
        int running = 0;
        for (int playerIndex = 0; playerIndex < 2; playerIndex++)
        {
            List<GameEvent> side = new List<GameEvent>();
            foreach (GameEvent draw in draws)
                if (draw.PlayerIndex == playerIndex)
                    side.Add(draw);
            if (side.Count == 0)
                continue;
            running++;
            StartCoroutine(PlayPlayerDraws(side, duration,
                () => running--));
        }
        while (running > 0)
            yield return null;
    }

    private IEnumerator PlayPlayerDraws(
        IReadOnlyList<GameEvent> draws, float duration, Action onComplete)
    {
        foreach (GameEvent draw in draws)
            yield return MoveCardTo(draw.CardInstanceId, CardZone.Hand,
                draw.PlayerIndex, 0,
                draw.PlayerIndex != before.ViewingPlayer, duration);
        onComplete?.Invoke();
    }

    private IEnumerator MoveDrawAndSignal(
        long instanceId, int playerIndex, float duration, Action onComplete)
    {
        yield return MoveCardTo(instanceId, CardZone.Hand, playerIndex, 0,
            playerIndex != before.ViewingPlayer, duration);
        onComplete?.Invoke();
    }

    private IEnumerator PlayEvent(GameEvent eventData, GameState finalState)
    {
        switch (eventData.Type)
        {
            case GameEventType.CardDrawn:
            case GameEventType.CardAddedToHand:
                // Draw events are grouped in PlaySequence so both players can
                // move simultaneously without reordering either hand.
                break;
            case GameEventType.MonsterSummoned:
                yield return MoveCardTo(eventData.CardInstanceId,
                    CardZone.MonsterZone, eventData.PlayerIndex,
                    eventData.Amount, false);
                break;
            case GameEventType.CardPlacedInSpellTrapZone:
                break;
            case GameEventType.FieldSpellActivated:
                yield return MoveCardTo(eventData.CardInstanceId,
                    CardZone.FieldZone, eventData.PlayerIndex, 0, false);
                break;
            case GameEventType.CardSet:
                yield return SetFace(eventData.CardInstanceId, true);
                break;
            case GameEventType.SpellActivated:
            case GameEventType.TrapActivated:
                yield return SetFace(eventData.CardInstanceId, false);
                yield return Pulse(CardPosition(eventData.CardInstanceId),
                    new Color32(100, 210, 255, 220));
                break;
            case GameEventType.MonsterTributed:
            case GameEventType.MonsterDestroyed:
            case GameEventType.CardSentToGraveyard:
                yield return MoveCardTo(eventData.CardInstanceId,
                    CardZone.Graveyard, eventData.PlayerIndex, 0, false);
                break;
            case GameEventType.AttackDeclared:
                pendingAttackerId = eventData.CardInstanceId;
                pendingAttackTargetId = eventData.TargetInstanceId;
                pendingAttackLunged = false;
                yield return Pulse(CardPosition(pendingAttackerId),
                    new Color32(255, 210, 45, 220));
                break;
            case GameEventType.MonsterDamaged:
                yield return LungeForDamage(eventData.CardInstanceId);
                yield return ChangeMonsterHealth(eventData.CardInstanceId,
                    -eventData.Amount);
                break;
            case GameEventType.PlayerDamaged:
                if (pendingAttackTargetId == 0)
                    yield return LungeForDamage(0);
                yield return ChangePlayerHealth(eventData.PlayerIndex,
                    -eventData.Amount);
                break;
            case GameEventType.MonsterHealthModified:
                yield return ChangeMonsterHealth(eventData.CardInstanceId,
                    eventData.Amount);
                break;
            case GameEventType.MonsterMaximumHealthModified:
                visibleMonsterMaximumHealth.TryGetValue(eventData.CardInstanceId, out int maximum);
                visibleMonsterMaximumHealth[eventData.CardInstanceId] = Mathf.Max(0, maximum + eventData.Amount);
                break;
            case GameEventType.EnergySpent:
                yield return ChangeEnergy(eventData.PlayerIndex,
                    -eventData.Amount);
                break;
            case GameEventType.EnergyRefreshed:
                yield return ChangeEnergy(eventData.PlayerIndex,
                    eventData.Amount - visibleEnergy[eventData.PlayerIndex]);
                break;
            case GameEventType.EnergyAllocated:
            case GameEventType.AllocatedEnergyRemoved:
                int delta = eventData.Type == GameEventType.EnergyAllocated
                    ? eventData.Amount : -eventData.Amount;
                ChangeMonsterEnergy(eventData.CardInstanceId, delta);
                break;
            case GameEventType.PhaseChanged:
                if (eventData.Phase == TurnPhase.Recovery)
                {
                    SetRecoveryHud(RecoveryStep.Draw);
                    break;
                }
                if (eventData.Phase == TurnPhase.Standby)
                    yield return WaitForRecoveryStep();
                SetPhaseHud(eventData.Phase + " Phase");
                if (eventData.Phase == TurnPhase.Main ||
                    eventData.Phase == TurnPhase.Battle)
                    yield return ShowBanner(eventData.Phase + " Phase");
                break;
            case GameEventType.TurnStarted:
                displayedTurnNumber = eventData.Amount;
                break;
            case GameEventType.RecoveryStepChanged:
                yield return WaitForRecoveryStep();
                SetRecoveryHud((RecoveryStep)eventData.Amount);
                break;
        }
    }

    private void SetRecoveryHud(RecoveryStep step)
    {
        SetPhaseHud("Recovery Phase\n" +
            (step == RecoveryStep.Draw ? "Draw Step" : "Energy Refresh Step"));
        recoveryStepStartedAt = Time.unscaledTime;
    }

    private IEnumerator WaitForRecoveryStep()
    {
        while (Time.unscaledTime - recoveryStepStartedAt < recoveryStepSeconds)
            yield return null;
    }

    private void SetPhaseHud(string phase)
    {
        Transform hud = transform.Find("SafeAreaRoot/HudLayer/TurnPhaseHUD");
        TMP_Text turn = hud?.Find("TurnText")?.GetComponent<TMP_Text>();
        TMP_Text phaseText = hud?.Find("PhaseText")?.GetComponent<TMP_Text>();
        if (turn != null)
            turn.text = "Turn " + displayedTurnNumber;
        if (phaseText != null)
        {
            phaseText.text = phase;
            phaseText.fontSize = phase.Contains("\n")
                ? Mathf.Min(phaseHudBaseFontSize, 22f)
                : phaseHudBaseFontSize;
        }
    }

    private IEnumerator MoveCardTo(
        long instanceId,
        CardZone zone,
        int playerIndex,
        int zoneIndex,
        bool faceDown,
        float duration = -1f)
    {
        RectTransform ghost = GetGhost(instanceId);
        if (ghost == null)
            yield break;

        if (zone == CardZone.Graveyard && before != null &&
            before.Cards.TryGetValue(instanceId, out CardBefore departing) &&
            departing.Zone == CardZone.MonsterZone &&
            departing.Visual != null)
        {
            Transform sourceZone = departing.Visual.parent != null
                ? departing.Visual.parent.parent : null;
            GameObject status = sourceZone?.Find("RuntimeMonsterStatus")
                ?.gameObject;
            if (status != null && status.activeSelf &&
                !hiddenGraveStatuses.Contains(status))
            {
                hiddenGraveStatuses.Add(status);
                status.SetActive(false);
            }
        }

        if (zone == CardZone.Hand && before != null &&
            !before.Cards.ContainsKey(instanceId))
        {
            ghost.anchoredPosition = ZonePosition(
                playerIndex, CardZone.Deck, 0);
            ghost.localScale = Vector3.one * 0.6f;
            ghost.GetComponent<CardView>()?.SetFaceDown(true);
        }

        Vector2 start = ghost.anchoredPosition;
        Vector2 end = zone == CardZone.Hand
            ? HandPosition(playerIndex, instanceId)
            : ZonePosition(playerIndex, zone, zoneIndex);
        float startScale = ghost.localScale.x;
        float endScale = zone == CardZone.Hand ? 0.7f :
            zone == CardZone.MonsterZone || zone == CardZone.SpellTrapZone ||
            zone == CardZone.Graveyard || zone == CardZone.Deck
                ? 0.6f : 1f;
        CardInstance card = FindCard(instanceId);
        float endAngle = zone == CardZone.MonsterZone && card != null &&
            card.BattlePosition == BattlePosition.Defense ? 90f : 0f;
        float startAngle = ghost.localEulerAngles.z;
        CardView view = ghost.GetComponent<CardView>();
        bool flip = view != null && view.IsFaceDown != faceDown;
        float moveSeconds = duration > 0f ? duration : cardMoveSeconds;
        float elapsed = 0f;
        while (elapsed < moveSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / moveSeconds);
            float curved = motionCurve.Evaluate(t);
            ghost.anchoredPosition = Vector2.LerpUnclamped(start, end, curved) +
                (zone == CardZone.Graveyard ? Vector2.zero :
                    Vector2.up * Mathf.Sin(t * Mathf.PI) * 35f);
            float scale = Mathf.Lerp(startScale, endScale, curved);
            if (flip)
            {
                float width = Mathf.Abs(1f - 2f * t);
                scale *= Mathf.Max(0.03f, width);
                if (t >= 0.5f && view.IsFaceDown != faceDown)
                    view.SetFaceDown(faceDown);
            }
            ghost.localScale = Vector3.one * scale;
            ghost.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.LerpAngle(startAngle, endAngle, curved));
            yield return null;
        }
        ghost.anchoredPosition = end;
        ghost.localScale = Vector3.one * endScale;
        ghost.localRotation = Quaternion.Euler(0f, 0f, endAngle);
        if (view != null)
            view.SetFaceDown(faceDown);
    }

    private IEnumerator SetFace(long instanceId, bool faceDown)
    {
        RectTransform ghost = GetGhost(instanceId);
        CardView view = ghost != null ? ghost.GetComponent<CardView>() : null;
        if (view == null || view.IsFaceDown == faceDown)
            yield break;

        float scale = ghost.localScale.x;
        float elapsed = 0f;
        while (elapsed < cardFlipSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / cardFlipSeconds);
            if (t >= 0.5f && view.IsFaceDown != faceDown)
                view.SetFaceDown(faceDown);
            Vector3 localScale = ghost.localScale;
            localScale.x = scale * Mathf.Max(0.03f, Mathf.Abs(1f - 2f * t));
            ghost.localScale = localScale;
            yield return null;
        }
        ghost.localScale = Vector3.one * scale;
        view.SetFaceDown(faceDown);
    }

    private IEnumerator LungeForDamage(long targetId)
    {
        if (pendingAttackerId == 0 || pendingAttackLunged ||
            (pendingAttackTargetId != targetId && targetId != 0))
            yield break;

        RectTransform ghost = GetGhost(pendingAttackerId);
        if (ghost == null)
            yield break;
        pendingAttackLunged = true;
        Vector2 start = ghost.anchoredPosition;
        CardInstance attacker = FindCard(pendingAttackerId);
        if (targetId == 0 && attacker == null)
            yield break;
        Vector2 target = targetId != 0
            ? CardPosition(targetId)
            : LabelPosition(PlayerLpLabel(1 - attacker.OwnerPlayerIndex));
        Vector2 impact = Vector2.Lerp(start, target, 0.72f);
        AttachMovingMonsterStatus(pendingAttackerId, start);
        float elapsed = 0f;
        while (elapsed < attackSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / attackSeconds);
            float travel = t < 0.55f
                ? Mathf.SmoothStep(0f, 1f, t / 0.55f)
                : Mathf.SmoothStep(1f, 0f, (t - 0.55f) / 0.45f);
            ghost.anchoredPosition = Vector2.Lerp(start, impact, travel);
            if (movingStatus != null)
                movingStatus.GetComponent<RectTransform>().anchoredPosition =
                    ghost.anchoredPosition;
            yield return null;
        }
        ghost.anchoredPosition = start;
        RestoreMovingMonsterStatus();
        yield return Pulse(target, new Color32(255, 230, 145, 245));
    }

    private void AttachMovingMonsterStatus(long instanceId, Vector2 start)
    {
        RestoreMovingMonsterStatus();
        if (before == null || !before.Cards.TryGetValue(instanceId,
                out CardBefore old) || old.Visual == null ||
            old.Zone != CardZone.MonsterZone)
            return;

        Transform zone = old.Visual.parent != null
            ? old.Visual.parent.parent : null;
        Transform source = zone?.Find("RuntimeMonsterStatus");
        if (source == null || !source.gameObject.activeSelf)
            return;

        hiddenStatus = source.gameObject;
        movingStatus = Instantiate(hiddenStatus, visualRoot);
        movingStatus.name = "RuntimeMovingMonsterStatus";
        RectTransform rect = movingStatus.GetComponent<RectTransform>();
        RectTransform sourceRect = source as RectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot =
            new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(Width(sourceRect), Height(sourceRect));
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = start;
        Canvas canvas = movingStatus.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = 17;
        foreach (Graphic graphic in movingStatus.GetComponentsInChildren<Graphic>())
            graphic.raycastTarget = false;
        hiddenStatus.SetActive(false);
    }

    private void RestoreMovingMonsterStatus()
    {
        if (hiddenStatus != null)
            hiddenStatus.SetActive(true);
        if (movingStatus != null)
        {
            movingStatus.SetActive(false);
            Destroy(movingStatus);
        }
        hiddenStatus = null;
        movingStatus = null;
    }

    private IEnumerator ChangeMonsterHealth(long instanceId, int delta)
    {
        if (!visibleMonsterHealth.TryGetValue(instanceId, out int oldValue))
            yield break;
        int newValue = Mathf.Max(0, oldValue + delta);
        visibleMonsterHealth[instanceId] = newValue;
        TMP_Text label = MonsterLabel(instanceId, "HealthText");
        Vector2 target = LabelPosition(label);
        Vector2 source = delta < 0 && pendingAttackerId != 0 &&
            pendingAttackTargetId == instanceId
            ? CardPosition(pendingAttackerId)
            : CardPosition(instanceId) + Vector2.up * 70f;
        yield return AnimateNumber(label, "HP ", oldValue, newValue,
            source, target, value => DuelMonsterZoneStatusView.SetHealthText(label, value,
                visibleMonsterMaximumHealth.TryGetValue(instanceId, out int maximum) ? maximum : newValue));
    }

    private IEnumerator ChangePlayerHealth(int playerIndex, int delta)
    {
        int oldValue = visibleHealth[playerIndex];
        int newValue = Mathf.Max(0, oldValue + delta);
        visibleHealth[playerIndex] = newValue;
        TMP_Text label = PlayerLpLabel(playerIndex);
        Vector2 target = LabelPosition(label);
        Vector2 source = pendingAttackerId != 0 && pendingAttackTargetId == 0
            ? CardPosition(pendingAttackerId)
            : target + Vector2.up * 70f;
        yield return AnimateNumber(label, "LP:", oldValue, newValue,
            source, target);
    }

    private IEnumerator ChangeEnergy(int playerIndex, int delta)
    {
        int oldValue = visibleEnergy[playerIndex];
        int newValue = Mathf.Max(0, oldValue + delta);
        visibleEnergy[playerIndex] = newValue;
        TMP_Text label = PlayerEnergyLabel(playerIndex);
        yield return AnimateNumber(label, "", oldValue, newValue,
            LabelPosition(label) + Vector2.up * 50f,
            LabelPosition(label));
    }

    private void ChangeMonsterEnergy(long instanceId, int delta)
    {
        if (!visibleMonsterEnergy.TryGetValue(instanceId, out int oldValue))
            return;
        int newValue = Mathf.Max(0, oldValue + delta);
        visibleMonsterEnergy[instanceId] = newValue;
        TMP_Text label = MonsterLabel(instanceId, "EnergyText");
        CardInstance card = FindCard(instanceId);
        if (label != null && card != null)
            label.text = "E " + newValue + "/" +
                card.Definition.AttackEnergyRequirement;
    }

    private IEnumerator AnimateNumber(
        TMP_Text label,
        string prefix,
        int oldValue,
        int newValue,
        Vector2 source,
        Vector2 target,
        Action<int> onValue = null)
    {
        int change = newValue - oldValue;
        if (change == 0)
        {
            onValue?.Invoke(newValue);
            yield break;
        }

        TextMeshProUGUI flying = CreateText(
            "RuntimeValueChange",
            (change > 0 ? "+" : "−") + Mathf.Abs(change),
            change > 0
                ? new Color32(108, 255, 160, 255)
                : new Color32(255, 105, 95, 255),
            48f);
        RectTransform rect = flying.rectTransform;
        rect.anchoredPosition = source;
        float elapsed = 0f;
        while (elapsed < numberSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / numberSeconds);
            rect.anchoredPosition = Vector2.Lerp(source, target, motionCurve.Evaluate(t));
            flying.color = new Color(flying.color.r, flying.color.g,
                flying.color.b, 1f - 0.65f * t);
            if (label != null)
                label.text = prefix + Mathf.RoundToInt(
                    Mathf.Lerp(oldValue, newValue, t));
            onValue?.Invoke(Mathf.RoundToInt(Mathf.Lerp(oldValue, newValue, t)));
            yield return null;
        }
        if (label != null)
            label.text = prefix + newValue;
        onValue?.Invoke(newValue);
        Destroy(flying.gameObject);
    }

    private IEnumerator Pulse(Vector2 position, Color color)
    {
        GameObject pulse = new GameObject("RuntimeImpact",
            typeof(RectTransform));
        pulse.layer = visualRoot.gameObject.layer;
        RectTransform rect = pulse.GetComponent<RectTransform>();
        rect.SetParent(visualRoot, false);
        rect.anchorMin = rect.anchorMax = rect.pivot =
            new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = position;
        Image flash = CreateImpactPart(rect, "Flash", Vector2.zero,
            new Vector2(75f, 75f), 45f);
        Image core = CreateImpactPart(rect, "Core", Vector2.zero,
            new Vector2(36f, 36f), 45f);
        Image[] rays = new Image[10];
        for (int index = 0; index < rays.Length; index++)
        {
            float angle = index * 360f / rays.Length;
            rays[index] = CreateImpactPart(rect, "Spark_" + index,
                Vector2.zero, new Vector2(12f, 65f), angle);
        }
        const float duration = 0.32f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = color.a * (1f - t);
            flash.rectTransform.localScale = Vector3.one *
                Mathf.Lerp(0.35f, 2.5f, t);
            flash.color = new Color(color.r, color.g, color.b,
                alpha * 0.55f);
            core.rectTransform.localScale = Vector3.one *
                Mathf.Lerp(0.4f, 1.4f, t);
            core.color = new Color(1f, 1f, 0.85f, alpha);
            for (int index = 0; index < rays.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / rays.Length;
                rays[index].rectTransform.anchoredPosition =
                    new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) *
                    Mathf.Lerp(10f, 105f, t);
                rays[index].color = new Color(color.r, color.g, color.b,
                    alpha * 0.9f);
            }
            yield return null;
        }
        pulse.SetActive(false);
        Destroy(pulse);
    }

    private static Image CreateImpactPart(
        RectTransform parent, string name, Vector2 position,
        Vector2 size, float rotation)
    {
        GameObject part = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        part.layer = parent.gameObject.layer;
        RectTransform rect = part.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot =
            new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        Image image = part.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private IEnumerator ShowBanner(string message)
    {
        TextMeshProUGUI text = CreateText(
            "RuntimePhaseBanner", message,
            new Color32(255, 224, 120, 255), 46f);
        RectTransform rect = text.rectTransform;
        rect.sizeDelta = new Vector2(650f, 90f);
        rect.anchoredPosition = Vector2.zero;
        float elapsed = 0f;
        while (elapsed < bannerSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / bannerSeconds);
            text.alpha = Mathf.Sin(t * Mathf.PI);
            rect.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.1f, t);
            yield return null;
        }
        Destroy(text.gameObject);
    }

    private TextMeshProUGUI CreateText(
        string name,
        string value,
        Color color,
        float size)
    {
        GameObject label = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        label.layer = visualRoot.gameObject.layer;
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.SetParent(visualRoot, false);
        rect.anchorMin = rect.anchorMax = rect.pivot =
            new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(240f, 95f);
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        if (font == null)
        {
            TMP_Text sample = transform.Find(
                "SafeAreaRoot/HudLayer/TurnPhaseHUD/TurnText")
                ?.GetComponent<TMP_Text>();
            font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
        }
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.color = color;
        text.text = value;
        return text;
    }

    private RectTransform GetGhost(long instanceId)
    {
        if (ghosts.TryGetValue(instanceId, out RectTransform existing))
            return existing;
        CardInstance instance = FindCard(instanceId);
        if (instance == null)
            return null;

        GameObject preview = board.CreateRuntimeCardPreview(instance, cardLayer);
        if (preview == null)
            return null;
        preview.name = "RuntimeMovingCard_" + instanceId;
        foreach (Graphic graphic in preview.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
        foreach (Button button in preview.GetComponentsInChildren<Button>(true))
            button.enabled = false;

        RectTransform rect = preview.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot =
            new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(250f, 350f);
        rect.anchoredPosition = CardPosition(instanceId);
        rect.localScale = Vector3.one * CardScale(instanceId);
        if (before.Cards.TryGetValue(instanceId, out CardBefore old))
        {
            preview.GetComponent<CardView>()?.SetFaceDown(old.FaceDown);
            if (old.Visual != null)
            {
                rect.localRotation = old.Visual.localRotation;
                old.Visual.gameObject.SetActive(false);
            }
        }
        ghosts.Add(instanceId, rect);
        return rect;
    }

    private CardInstance FindCard(long instanceId)
    {
        if (before != null && before.Cards.TryGetValue(
                instanceId, out CardBefore old))
            return old.Instance;
        return board.Session.State.FindCard(instanceId);
    }

    private Vector2 CardPosition(long instanceId)
    {
        if (ghosts.TryGetValue(instanceId, out RectTransform ghost))
            return ghost.anchoredPosition;
        if (before != null && before.Cards.TryGetValue(
                instanceId, out CardBefore old))
        {
            if (old.Visual != null)
                return Position(old.Visual);
            return ZonePosition(old.Owner, old.Zone, old.Index);
        }
        return Vector2.zero;
    }

    private float CardScale(long instanceId)
    {
        if (before != null && before.Cards.TryGetValue(
                instanceId, out CardBefore old))
        {
            if (old.Visual != null)
                return Mathf.Max(0.1f, Width(old.Visual) / 250f);
            if (old.Zone == CardZone.Hand)
                return 0.7f;
            if (old.Zone == CardZone.MonsterZone ||
                old.Zone == CardZone.SpellTrapZone ||
                old.Zone == CardZone.Deck ||
                old.Zone == CardZone.Graveyard)
                return 0.6f;
        }
        return 1f;
    }

    private Vector2 ZonePosition(int playerIndex, CardZone zone, int index)
    {
        string side = playerIndex == before.ViewingPlayer ? "Player" : "Opp";
        string prefix = "SafeAreaRoot/BoardLayer/";
        string path;
        switch (zone)
        {
            case CardZone.Deck:
                path = side + "_PileColumn/" + side + "_DeckZone/CardAnchor";
                break;
            case CardZone.Graveyard:
                path = side + "_PileColumn/" + side + "_GraveyardZone/CardAnchor";
                break;
            case CardZone.MonsterZone:
            case CardZone.SpellTrapZone:
                int shownIndex = side == "Opp" ? 2 - index : index;
                string row = zone == CardZone.MonsterZone
                    ? "_MonsterRow/" + side + "_MonsterZone_"
                    : "_SpellTrapRow/" + side + "_SpellTrapZone_";
                path = side + row + shownIndex + "/CardAnchor";
                break;
            case CardZone.FieldZone:
                path = side + "_FieldZone/CardAnchor";
                break;
            default:
                path = side + "_HandArea/HandContent";
                break;
        }
        Transform target = transform.Find(prefix + path);
        return target != null ? Position(target) : Vector2.zero;
    }

    private Vector2 HandPosition(int playerIndex, long instanceId)
    {
        string side = playerIndex == before.ViewingPlayer ? "Player" : "Opp";
        RectTransform content = transform.Find(
            "SafeAreaRoot/BoardLayer/" + side + "_HandArea/HandContent")
            as RectTransform;
        if (content == null)
            return Vector2.zero;

        IReadOnlyList<CardInstance> hand =
            board.Session.State.Players[playerIndex].Hand;
        int index = -1;
        for (int i = 0; i < hand.Count; i++)
            if (hand[i].InstanceId == instanceId)
            {
                index = i;
                break;
            }
        if (index < 0)
            return Position(content);

        float step = hand.Count > 1
            ? Mathf.Min(125f,
                Mathf.Max(0f, content.rect.width - 175f) / (hand.Count - 1))
            : 0f;
        int visualIndex = side == "Opp" ? hand.Count - 1 - index : index;
        return Position(content) +
            Vector2.right * ((visualIndex - (hand.Count - 1) * 0.5f) * step);
    }

    private TMP_Text MonsterLabel(long instanceId, string labelName)
    {
        if (before == null || !before.Cards.TryGetValue(
                instanceId, out CardBefore card) ||
            card.Zone != CardZone.MonsterZone)
            return null;
        string side = card.Owner == before.ViewingPlayer ? "Player" : "Opp";
        int index = side == "Opp" ? 2 - card.Index : card.Index;
        Transform target = transform.Find(
            "SafeAreaRoot/BoardLayer/" + side + "_MonsterRow/" +
            side + "_MonsterZone_" + index +
            "/RuntimeMonsterStatus/" + labelName);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private TMP_Text PlayerLpLabel(int playerIndex)
    {
        string side = playerIndex == before.ViewingPlayer ? "Player" : "Opp";
        Transform target = transform.Find(
            "SafeAreaRoot/HudLayer/" + side + "_HUD/LpText");
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private TMP_Text PlayerEnergyLabel(int playerIndex)
    {
        string side = playerIndex == before.ViewingPlayer ? "Player" : "Opp";
        Transform target = transform.Find(
            "SafeAreaRoot/HudLayer/" + side + "_EnergyWidget/ValueText");
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private Vector2 LabelPosition(TMP_Text label)
    {
        return label != null ? Position(label.rectTransform) : Vector2.zero;
    }

    private Vector2 Position(Transform target)
    {
        RectTransform rect = target as RectTransform;
        Vector3 world = rect != null
            ? rect.TransformPoint(rect.rect.center)
            : target.position;
        return visualRoot.InverseTransformPoint(world);
    }

    private float Width(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Vector3.Distance(
            visualRoot.InverseTransformPoint(corners[0]),
            visualRoot.InverseTransformPoint(corners[3]));
    }

    private float Height(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Vector3.Distance(
            visualRoot.InverseTransformPoint(corners[0]),
            visualRoot.InverseTransformPoint(corners[1]));
    }

    private void ClearGhosts()
    {
        RestoreMovingMonsterStatus();
        foreach (RectTransform ghost in ghosts.Values)
            if (ghost != null)
            {
                ghost.gameObject.SetActive(false);
                Destroy(ghost.gameObject);
            }
        ghosts.Clear();
    }

    private void OnDisable()
    {
        ClearGhosts();
        foreach (GameObject status in hiddenGraveStatuses)
            if (status != null)
                status.SetActive(true);
        hiddenGraveStatuses.Clear();
        if (before != null)
            foreach (CardBefore card in before.Cards.Values)
                if (card.Visual != null)
                    card.Visual.gameObject.SetActive(true);
        pendingEvents.Clear();
        before = null;
        IsPlaying = false;
        if (blocker != null)
            blocker.SetActive(false);
    }
}

// A restrained, input-transparent frame for monsters that have a legal Attack.
public sealed class DuelAttackReadyGlow : MonoBehaviour
{
    private readonly List<Image> edges = new List<Image>();

    public static GameObject Create(Transform parent)
    {
        GameObject root = new GameObject(
            "RuntimeAttackReadyGlow", typeof(RectTransform),
            typeof(Canvas), typeof(DuelAttackReadyGlow));
        root.layer = parent.gameObject.layer;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot =
            new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(270f, 370f);
        rect.SetAsFirstSibling();
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 16;

        DuelAttackReadyGlow glow = root.GetComponent<DuelAttackReadyGlow>();
        glow.AddEdge("Top", new Vector2(0f, 185f),
            new Vector2(270f, 8f));
        glow.AddEdge("Bottom", new Vector2(0f, -185f),
            new Vector2(270f, 8f));
        glow.AddEdge("Left", new Vector2(-135f, 0f),
            new Vector2(8f, 370f));
        glow.AddEdge("Right", new Vector2(135f, 0f),
            new Vector2(8f, 370f));
        return root;
    }

    private void AddEdge(string name, Vector2 position, Vector2 size)
    {
        GameObject edge = new GameObject(
            name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        edge.layer = gameObject.layer;
        RectTransform rect = edge.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot =
            new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = edge.GetComponent<Image>();
        image.raycastTarget = false;
        edges.Add(image);
    }

    private void Update()
    {
        float alpha = 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
        foreach (Image edge in edges)
            edge.color = new Color(1f, 0.78f, 0.1f, alpha);
    }
}
