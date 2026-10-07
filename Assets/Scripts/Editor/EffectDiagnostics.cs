using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EffectDiagnostics
{
    private const uint TestSeed = 20261001u;

    [MenuItem("Card Game/Diagnostics/Run Effect Pipeline Smoke Test")]
    public static void RunEffectPipelineSmokeTest()
    {
        GameState state = CreateState();
        int playerIndex = state.CurrentPlayerIndex;
        PlayerState player = state.Players[playerIndex];
        int energyBeforeInvalidRequest = player.AvailableEnergy;

        EffectContext invalidContext = new EffectContext(
            state,
            playerIndex,
            0,
            new long[] { 999 });
        ActionResult invalidResult = EffectResolver.TryResolve(
            invalidContext,
            new AlwaysCondition(),
            new AvailableEnergyCost(2),
            new NoTargetRule(),
            new SkipNextDrawEffect());

        if (invalidResult.Succeeded ||
            player.AvailableEnergy != energyBeforeInvalidRequest ||
            player.IsDrawStepSkipScheduled(state.TurnNumber + 2))
        {
            Debug.LogError(
                "Effect pipeline changed state after invalid targeting.");
            return;
        }

        EffectContext validContext = new EffectContext(
            state,
            playerIndex,
            0);
        ActionResult validResult = EffectResolver.TryResolve(
            validContext,
            new AlwaysCondition(),
            new AvailableEnergyCost(2),
            new NoTargetRule(),
            new SkipNextDrawEffect());

        if (!validResult.Succeeded ||
            player.AvailableEnergy != energyBeforeInvalidRequest - 2 ||
            !player.IsDrawStepSkipScheduled(state.TurnNumber + 2))
        {
            Debug.LogError(
                "Effect pipeline did not pay and resolve in the expected order.");
            return;
        }

        Debug.Log(
            "Effect pipeline smoke test passed.\n" +
            "Invalid target paid no cost: True\n" +
            "Valid request paid 2 Energy: True\n" +
            "Next own Draw Step marked to skip: True\n" +
            $"Generated events: {validResult.Events.Count}");
    }

    [MenuItem("Card Game/Diagnostics/Run Skip Opponent Draw Smoke Test")]
    public static void RunSkipOpponentDrawSmokeTest()
    {
        EffectResolutionAsset component =
            AssetDatabase.LoadAssetAtPath<EffectResolutionAsset>(
                "Assets/Data/EffectComponents/Resolutions/" +
                "Skip_Next_Opponent_Draw.asset");
        if (component == null)
        {
            Debug.LogError("Skip Next Opponent Draw component asset is missing.");
            return;
        }
        EffectOperationDefinition operation = component.CreateDefinition(1);
        if (operation.Type != EffectOperationType.SkipNextOpponentDrawStep)
        {
            Debug.LogError("Skip Next Opponent Draw asset has the wrong operation.");
            return;
        }

        EffectDefinition definition = new EffectDefinition(
            "diagnostic_skip_opponent_draw",
            EffectTriggerType.ManualActivation,
            EffectKind.Activated,
            EffectPhaseMask.Main,
            EffectTurnScope.EitherTurn,
            EffectActivationWindow.OpenGameState,
            null,
            null,
            EffectTargetType.None,
            0,
            new[] { operation });
        DefinitionCardEffect effect = new DefinitionCardEffect(definition);
        GameState state = CreateState();
        int firstPlayer = state.CurrentPlayerIndex;
        int opponent = 1 - firstPlayer;
        EffectContext context = new EffectContext(state, firstPlayer, 0);

        ActionResult firstUse = EffectResolver.TryResolve(
            context, new AlwaysCondition(), new NoCost(),
            new NoTargetRule(), effect);
        ActionResult repeatedUse = EffectResolver.TryResolve(
            context, new AlwaysCondition(), new NoCost(),
            new NoTargetRule(), effect);
        bool scheduledOnce = firstUse.Succeeded && repeatedUse.Succeeded &&
            state.Players[opponent].IsDrawStepSkipScheduled(2) &&
            !state.Players[opponent].IsDrawStepSkipScheduled(4);

        ApplyRequired(state, GameAction.ResolveDrawStep(firstPlayer));
        FinishTurnAfterDraw(state, firstPlayer);

        bool unresolvedDrawIsCurrent =
            state.GetNextDrawTurnForPlayer(opponent) == state.TurnNumber;
        PlayerState opponentState = state.Players[opponent];
        int handBefore = opponentState.Hand.Count;
        int deckBefore = opponentState.Deck.Count;
        ActionResult firstSkippedDraw =
            ApplyRequired(state, GameAction.ResolveDrawStep(opponent));
        bool skippedOnTurnTwo =
            firstSkippedDraw.Events.Count > 0 &&
            firstSkippedDraw.Events[0].Type == GameEventType.DrawStepSkipped &&
            opponentState.Hand.Count == handBefore &&
            opponentState.Deck.Count == deckBefore &&
            !opponentState.IsDrawStepSkipScheduled(2);

        ApplyRequired(state, GameAction.ResolveEnergyRefreshStep(opponent));
        ApplyRequired(state, GameAction.AdvancePhase(opponent));
        ActionResult opponentTurnUse = EffectResolver.TryResolve(
            new EffectContext(state, firstPlayer, 0),
            new AlwaysCondition(), new NoCost(),
            new NoTargetRule(), effect);
        bool opponentTurnScheduled = opponentTurnUse.Succeeded &&
            state.GetNextDrawTurnForPlayer(opponent) == 4 &&
            opponentState.IsDrawStepSkipScheduled(4);

        ApplyRequired(state, GameAction.EnterEndPhase(opponent));
        ApplyRequired(state, GameAction.EndTurn(opponent));
        ApplyRequired(state, GameAction.ResolveDrawStep(firstPlayer));
        FinishTurnAfterDraw(state, firstPlayer);

        handBefore = opponentState.Hand.Count;
        deckBefore = opponentState.Deck.Count;
        ActionResult secondSkippedDraw =
            ApplyRequired(state, GameAction.ResolveDrawStep(opponent));
        bool skippedOnTurnFour =
            secondSkippedDraw.Events.Count > 0 &&
            secondSkippedDraw.Events[0].Type == GameEventType.DrawStepSkipped &&
            opponentState.Hand.Count == handBefore &&
            opponentState.Deck.Count == deckBefore &&
            !opponentState.IsDrawStepSkipScheduled(4);

        FinishTurnAfterDraw(state, opponent);
        ApplyRequired(state, GameAction.ResolveDrawStep(firstPlayer));
        FinishTurnAfterDraw(state, firstPlayer);
        handBefore = opponentState.Hand.Count;
        deckBefore = opponentState.Deck.Count;
        ActionResult laterDraw =
            ApplyRequired(state, GameAction.ResolveDrawStep(opponent));
        bool laterDrawRestored =
            laterDraw.Events.Count > 0 &&
            laterDraw.Events[0].Type == GameEventType.CardDrawn &&
            opponentState.Hand.Count == handBefore + 1 &&
            opponentState.Deck.Count == deckBefore - 1;

        GameState beforeDrawState = CreateState();
        int beforeDrawActor = beforeDrawState.CurrentPlayerIndex;
        int beforeDrawOpponent = 1 - beforeDrawActor;
        ApplyRequired(beforeDrawState, GameAction.ResolveDrawStep(beforeDrawActor));
        FinishTurnAfterDraw(beforeDrawState, beforeDrawActor);
        ActionResult beforeDrawUse = EffectResolver.TryResolve(
            new EffectContext(beforeDrawState, beforeDrawActor, 0),
            new AlwaysCondition(), new NoCost(),
            new NoTargetRule(), effect);
        ActionResult currentDraw = ApplyRequired(
            beforeDrawState, GameAction.ResolveDrawStep(beforeDrawOpponent));
        bool currentUnresolvedDrawSkipped = beforeDrawUse.Succeeded &&
            currentDraw.Events.Count > 0 &&
            currentDraw.Events[0].Type == GameEventType.DrawStepSkipped;

        if (!scheduledOnce || !unresolvedDrawIsCurrent ||
            !skippedOnTurnTwo || !opponentTurnScheduled ||
            !skippedOnTurnFour || !laterDrawRestored ||
            !currentUnresolvedDrawSkipped)
        {
            Debug.LogError("Skip Opponent Draw produced an incorrect turn sequence.");
            return;
        }

        Debug.Log(
            "Skip Opponent Draw smoke test passed.\n" +
            "Activation on own turn skips opponent's next Draw Step: True\n" +
            "Repeated skips for one turn do not stack: True\n" +
            "Activation on opponent's turn targets their next unresolved Draw Step: True\n" +
            "The marked Draw Steps were actually skipped: True\n" +
            "A later unscheduled Draw Step works normally: True");
    }

    private static ActionResult ApplyRequired(GameState state, GameAction action)
    {
        ActionResult result = GameRules.ApplyAction(state, action);
        if (!result.Succeeded)
            throw new System.InvalidOperationException(
                "Required action failed: " + action + ". " + result.ErrorMessage);
        return result;
    }

    private static void FinishTurnAfterDraw(GameState state, int playerIndex)
    {
        ApplyRequired(state, GameAction.ResolveEnergyRefreshStep(playerIndex));
        if (state.CurrentPhase == TurnPhase.Standby)
            ApplyRequired(state, GameAction.AdvancePhase(playerIndex));
        ApplyRequired(state, GameAction.EnterEndPhase(playerIndex));
        ApplyRequired(state, GameAction.EndTurn(playerIndex));
    }

    private static GameState CreateState()
    {
        GameConfig config = GameConfig.CreateDefault();
        CardDefinition card = new CardDefinition(
            "diagnostic_effect_card",
            "Diagnostic Effect Card",
            CardCategory.Spell,
            "Used only by the in-memory effect pipeline smoke test.",
            MonsterAttribute.Metal,
            MonsterRank.Basic,
            MonsterType.Spellcaster,
            0,
            0,
            0,
            0,
            SpellType.Normal,
            TrapType.Normal,
            0,
            CardSpeed.Speed1);
        List<CardDefinition> firstDeck = BuildDeck(
            card,
            config.MinimumDeckSize);
        List<CardDefinition> secondDeck = BuildDeck(
            card,
            config.MinimumDeckSize);
        return GameState.CreateNewMatch(
            config,
            TestSeed,
            firstDeck,
            secondDeck);
    }

    private static List<CardDefinition> BuildDeck(
        CardDefinition card,
        int count)
    {
        List<CardDefinition> deck = new List<CardDefinition>(count);

        for (int index = 0; index < count; index++)
        {
            deck.Add(card);
        }

        return deck;
    }

    private sealed class AlwaysCondition : IEffectCondition
    {
        public bool IsMet(
            EffectContext context,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class AvailableEnergyCost : IEffectCost
    {
        private readonly int amount;

        public AvailableEnergyCost(int amount)
        {
            this.amount = amount;
        }

        public bool CanPay(
            EffectContext context,
            out string failureReason)
        {
            bool canPay = context.ActivatingPlayer.AvailableEnergy >= amount;
            failureReason = canPay
                ? string.Empty
                : "Not enough Available Energy to pay the effect cost.";
            return canPay;
        }

        public void Pay(
            EffectContext context,
            List<GameEvent> events)
        {
            context.ActivatingPlayer.TrySpendEnergy(amount);
            events.Add(GameEvent.EnergySpent(
                context.ActivatingPlayerIndex,
                amount));
        }
    }

    private sealed class NoCost : IEffectCost
    {
        public bool CanPay(EffectContext context, out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public void Pay(EffectContext context, List<GameEvent> events) { }
    }

    private sealed class NoTargetRule : IEffectTargetRule
    {
        public bool AreTargetsValid(
            EffectContext context,
            out string failureReason)
        {
            bool valid = context.TargetInstanceIds.Count == 0;
            failureReason = valid
                ? string.Empty
                : "This effect does not accept a target.";
            return valid;
        }
    }

    private sealed class SkipNextDrawEffect : ICardEffect
    {
        public void Resolve(
            EffectContext context,
            List<GameEvent> events)
        {
            int nextOwnTurn = context.State.GetNextDrawTurnForPlayer(
                context.ActivatingPlayerIndex);
            context.ActivatingPlayer.ScheduleDrawStepSkip(nextOwnTurn);
            events.Add(GameEvent.EffectResolved(
                context.ActivatingPlayerIndex,
                context.SourceInstanceId,
                context.State.CurrentPhase));
        }
    }
}
