#if UNITY_EDITOR || NORMAL_BOT_HEADLESS
using System;
using System.Collections.Generic;
using System.Reflection;

public static class NormalBotVerification
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Card Game/Diagnostics/Verify Normal Bot")]
#endif
    public static void Verify()
    {
        VerifyAttackOrder();
        VerifyHealing();
        VerifyEnergy();
        VerifyMatches();
#if UNITY_EDITOR
        UnityEngine.Debug.Log("Normal Bot PASS: attack order, healing, Energy and 6 automated matches.");
#else
        Console.WriteLine("Normal Bot PASS: attack order, healing, Energy and 6 automated matches.");
#endif
    }

    private static CardDefinition Monster(string id, int attack = 1000) => new CardDefinition(
        id, id, CardCategory.Monster, "", MonsterAttribute.Wood, MonsterRank.Basic,
        MonsterType.Fairy, 1000, attack, 300, 1, SpellType.Normal, TrapType.Normal, 0, CardSpeed.Speed1);

    private static GameState State(CardDefinition definition)
    {
        var deck = new List<CardDefinition>();
        for (int i = 0; i < 30; i++) deck.Add(definition);
        GameState state = GameState.CreateNewMatch(GameConfig.CreateDefault(), 42, deck, deck);
        Internal(state, "StartNextTurn");
        Internal(state, "SetCurrentPhase", TurnPhase.Main);
        return state;
    }

    private static CardInstance Place(GameState state, int player, int zone, long id, int attack)
    {
        var card = new CardInstance(id, player, Monster("unit_" + id, attack));
        Internal(state.Players[player], "PlaceMonsterFromHand", card, zone, BattlePosition.Attack);
        card.AllocateEnergy(1); card.ResetTurnFlags();
        return card;
    }

    private static GameAction Choose(NormalBot bot, GameState state)
    {
        int player = state.InteractionPlayerIndex;
        var legal = GameRules.GetLegalActions(state, player);
        Require(legal.Count > 0, "legal actions exist");
        bot.Observe(state, player);
        GameAction action = bot.ChooseAction(BotGameStateView.Create(state, player), legal);
        bool found = false;
        foreach (GameAction candidate in legal) if (ReferenceEquals(candidate, action)) found = true;
        Require(found, "chosen action belongs to supplied legal list");
        return action;
    }

    private static void VerifyAttackOrder()
    {
        GameState state = State(Monster("deck"));
        int player = state.CurrentPlayerIndex;
        Place(state, player, 0, 1001, 1200);
        Place(state, player, 1, 1002, 600);
        CardInstance target = Place(state, 1 - player, 0, 1003, 100);
        target.ReceiveDamage(500);
        state.Players[1 - player].TakeDamage(4400);
        Internal(state, "SetCurrentPhase", TurnPhase.Battle);
        GameAction action = Choose(new NormalBot(), state);
        Require(action.Type == GameActionType.Attack && action.SourceInstanceId == 1002,
            "weak attacker clears blocker to preserve winning direct attack");
    }

    private static void VerifyHealing()
    {
        var effect = new EffectDefinition("heal", EffectTriggerType.ManualActivation, EffectKind.Activated,
            EffectPhaseMask.Main, EffectTurnScope.YourTurn, EffectActivationWindow.OpenGameState,
            EffectActivationSource.Standard, null, null, EffectTargetType.OwnMonster, 1,
            new[] { new EffectOperationDefinition(EffectOperationType.RestoreMonsterHealth, 400) });
        var spell = new CardDefinition("heal", "heal", CardCategory.Spell, "", MonsterAttribute.Wood,
            MonsterRank.Basic, MonsterType.Fairy, 0, 0, 0, 0, SpellType.Normal, TrapType.Normal,
            0, CardSpeed.Speed1, new[] { effect });
        GameState state = State(spell);
        CardInstance target = Place(state, state.CurrentPlayerIndex, 0, 1001, 1000);
        var bot = new NormalBot();
        Require(Choose(bot, state).Type != GameActionType.ActivateCard, "do not waste healing at full HP");
        target.ReceiveDamage(700);
        Require(Choose(bot, state).Type == GameActionType.ActivateCard, "use effective healing");
    }

    private static void VerifyEnergy()
    {
        GameState state = State(Monster("deck"));
        CardInstance card = Place(state, state.CurrentPlayerIndex, 0, 1001, 1800);
        card.ClearAllocatedEnergy();
        GameAction action = Choose(new NormalBot(), state);
        Require(action.Type == GameActionType.AllocateEnergy && action.Amount == 1,
            "fund a useful attack without overallocating");
    }

    private static void VerifyMatches()
    {
        for (uint seed = 1; seed <= 6; seed++)
        {
            var deck = new List<CardDefinition>();
            for (int i = 0; i < 30; i++) deck.Add(Monster("unit_" + i, 500 + i % 4 * 300));
            GameSession session = GameSession.Create(GameConfig.CreateDefault(), seed, deck, deck);
            var normal = new NormalBot(); var easy = new EasyBot();
            for (int step = 0; step < 1200 && !session.State.IsFinished; step++)
            {
                GameState state = session.State;
                int player = state.InteractionPlayerIndex;
                GameAction action;
                if (!state.HasOpenResponseWindow && !state.HasPendingTriggeredEffect && state.CurrentPhase == TurnPhase.Recovery)
                    action = state.CurrentRecoveryStep == RecoveryStep.Draw
                        ? GameAction.ResolveDrawStep(player) : GameAction.ResolveEnergyRefreshStep(player);
                else if (!state.HasOpenResponseWindow && !state.HasPendingTriggeredEffect && state.CurrentPhase == TurnPhase.Standby)
                    action = GameAction.AdvancePhase(player);
                else if (seed > 3 || player == (int)(seed % 2)) action = Choose(normal, state);
                else action = easy.ChooseAction(BotGameStateView.Create(state, player), GameRules.GetLegalActions(state, player));
                ActionResult result = session.TryApply(action);
                Require(result.Succeeded, result.ErrorMessage);
            }
            Require(session.State.IsFinished, "automated match finishes without stalling, seed " + seed);
        }
    }

    // Editor tests are compiled separately from runtime code in Unity.
    // Reflection is restricted to fixture setup; decisions still use public APIs.
    private static void Internal(object target, string method, params object[] args)
    {
        MethodInfo member = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        if (member == null) throw new MissingMethodException(target.GetType().Name, method);
        member.Invoke(target, args);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Normal Bot: " + message);
    }
}
#endif
