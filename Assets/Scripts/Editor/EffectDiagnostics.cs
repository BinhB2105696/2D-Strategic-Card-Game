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
            player.PendingDrawStepSkips != 0)
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
            player.PendingDrawStepSkips != 1)
        {
            Debug.LogError(
                "Effect pipeline did not pay and resolve in the expected order.");
            return;
        }

        Debug.Log(
            "Effect pipeline smoke test passed.\n" +
            "Invalid target paid no cost: True\n" +
            "Valid request paid 2 Energy: True\n" +
            "Skip-next-Draw effect queued: True\n" +
            $"Generated events: {validResult.Events.Count}");
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
            context.ActivatingPlayer.AddDrawStepSkips(1);
            events.Add(GameEvent.EffectResolved(
                context.ActivatingPlayerIndex,
                context.SourceInstanceId,
                context.State.CurrentPhase));
        }
    }
}
