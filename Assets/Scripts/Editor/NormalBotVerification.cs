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
        VerifyDefensivePositions();
        VerifySelfEffects();
        VerifyEquip();
        VerifyTributeValue();
        VerifyThreatTarget();
        VerifyMatches();
#if UNITY_EDITOR
        UnityEngine.Debug.Log("Normal Bot PASS: attack order, self effects, Equip, positions, tributes, targets, Energy and 6 automated matches.");
#else
        Console.WriteLine("Normal Bot PASS: attack order, self effects, Equip, positions, tributes, targets, Energy and 6 automated matches.");
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

    private static CardDefinition TacticalMonster(string id, int hp, int attack, int defense,
        EffectDefinition effect = null, MonsterRank rank = MonsterRank.Basic, int tributes = 0, int attackEnergy = 1)
        => new CardDefinition(id, id, CardCategory.Monster, "", MonsterAttribute.Wood, rank,
            MonsterType.Fairy, hp, attack, defense, attackEnergy, SpellType.Normal, TrapType.Normal,
            tributes, CardSpeed.Speed1, effect == null ? null : new[] { effect });

    private static CardInstance PlaceDefinition(GameState state, int player, int zone, long id,
        CardDefinition definition, BattlePosition position = BattlePosition.Attack)
    {
        var card = new CardInstance(id, player, definition);
        Internal(state.Players[player], "PlaceMonsterFromHand", card, zone, position);
        card.AllocateEnergy(definition.AttackEnergyRequirement);
        card.ResetTurnFlags();
        return card;
    }

    private static EffectDefinition SelfEffect(EffectOperationType operation, int amount)
        => new EffectDefinition("self", EffectTriggerType.ManualActivation, EffectKind.Activated,
            EffectPhaseMask.Main, EffectTurnScope.YourTurn, EffectActivationWindow.OpenGameState,
            EffectActivationSource.Standard, null, new[] { new EffectCostDefinition(EffectCostType.AvailableEnergy, 1) },
            EffectTargetType.None, 0, new[] { new EffectOperationDefinition(operation, amount) }, 1);

    private static void FillOwnZones(GameState state)
    {
        for (int i = 1; i < 3; i++) Place(state, state.CurrentPlayerIndex, i, 1100 + i, 100);
    }

    private static void VerifyDefensivePositions()
    {
        CardDefinition definition = Monster("defense");
        var deck = new List<CardDefinition>();
        for (int i = 0; i < 30; i++) deck.Add(definition);
        GameState state = GameState.CreateNewMatch(GameConfig.CreateDefault(), 42, deck, deck);
        Internal(state, "SetCurrentPhase", TurnPhase.Main);
        GameAction action = Choose(new NormalBot(), state);
        Require(action.Type == GameActionType.SummonMonster && action.BattlePosition == BattlePosition.Defense,
            "first-turn summon defends");
        state = State(definition);
        state.Players[state.CurrentPlayerIndex].TrySpendEnergy(4);
        action = Choose(new NormalBot(), state);
        Require(action.Type == GameActionType.SummonMonster && action.BattlePosition == BattlePosition.Defense,
            "summon defends when summon cost leaves no attack Energy");
        state = State(definition);
        CardInstance card = Place(state, state.CurrentPlayerIndex, 0, 1001, 1000);
        card.ClearAllocatedEnergy();
        FillOwnZones(state);
        action = Choose(new NormalBot(), state);
        Require(action.Type == GameActionType.AllocateEnergy && action.SourceInstanceId == card.InstanceId,
            "fund a possible attack before deciding to defend");
        state.Players[state.CurrentPlayerIndex].TrySpendEnergy(5);
        action = Choose(new NormalBot(), state);
        Require(action.Type == GameActionType.ChangeBattlePosition && action.SourceInstanceId == card.InstanceId &&
            action.BattlePosition == BattlePosition.Defense, "unfundable monster changes to defense");
    }

    private static void VerifySelfEffects()
    {
        var operations = new[] { EffectOperationType.IncreaseAttackUntilEndOfTurn,
            EffectOperationType.IncreaseMaximumHealth, EffectOperationType.IncreaseDefensePermanently };
        for (int i = 0; i < operations.Length; i++)
        {
            GameState state = State(Monster("deck"));
            int player = state.CurrentPlayerIndex;
            var definition = TacticalMonster("self_unit", i == 0 ? 1000 : 600, i == 0 ? 1000 : 100,
                i == 2 ? 300 : 0, SelfEffect(operations[i], i == 0 ? 300 : i == 1 ? 200 : 100));
            CardInstance source = PlaceDefinition(state, player, 0, 1001, definition,
                i == 2 ? BattlePosition.Defense : BattlePosition.Attack);
            FillOwnZones(state);
            PlaceDefinition(state, 1 - player, 0, 2001,
                TacticalMonster("threat", i == 0 ? 1300 : 1000, i == 2 ? 950 : 700, 0));
            int hp = source.CurrentHealth, attack = source.CurrentAttack, defense = source.CurrentDefense;
            GameAction action = Choose(new NormalBot(), state);
            Require(action.Type == GameActionType.ActivateCard && action.SourceInstanceId == source.InstanceId,
                "use valuable self effect " + operations[i]);
            Require(source.CurrentHealth == hp && source.CurrentAttack == attack && source.CurrentDefense == defense,
                "observation/scoring does not mutate live card");
        }
    }

    private static void VerifyEquip()
    {
        var effect = new EffectDefinition("equip", EffectTriggerType.ManualActivation, EffectKind.Activated,
            EffectPhaseMask.Main, EffectTurnScope.YourTurn, EffectActivationWindow.OpenGameState,
            EffectActivationSource.Standard, null, new[] { new EffectCostDefinition(EffectCostType.AvailableEnergy, 1) },
            EffectTargetType.OwnMonster, 1, null, 1, continuousModifiers: new IContinuousModifier[] { new AttackContinuousModifier(300) });
        var spell = new CardDefinition("equip", "equip", CardCategory.Spell, "", MonsterAttribute.Wood,
            MonsterRank.Basic, MonsterType.Fairy, 0, 0, 0, 0, SpellType.Equip, TrapType.Normal,
            0, CardSpeed.Speed1, new[] { effect });
        GameState state = State(spell);
        CardInstance target = Place(state, state.CurrentPlayerIndex, 0, 1001, 1000);
        FillOwnZones(state);
        PlaceDefinition(state, 1 - state.CurrentPlayerIndex, 0, 2001, TacticalMonster("blocker", 1300, 300, 0));
        GameAction action = Choose(new NormalBot(), state);
        Require(action.Type == GameActionType.ActivateCard && action.TargetInstanceId == target.InstanceId,
            "Equip is valued by its actual ATK bonus and KO threshold");
    }

    private static void VerifyTributeValue()
    {
        var effect = new EffectDefinition("tribute_power", EffectTriggerType.OnSummoned, EffectKind.Activated,
            EffectPhaseMask.Main, EffectTurnScope.YourTurn, EffectActivationWindow.OpenGameState,
            EffectActivationSource.Standard, null, null, EffectTargetType.None, 0,
            new[] { new EffectOperationDefinition(EffectOperationType.IncreaseAttackByTriggerValue, 0) });
        GameState state = State(TacticalMonster("tribute_boss", 3500, 1000, 800, effect, MonsterRank.Ultimate, 3, 2));
        int player = state.CurrentPlayerIndex;
        for (int i = 0; i < 3; i++) Place(state, player, i, 1001 + i, 1000);
        PlaceDefinition(state, 1 - player, 0, 2001, TacticalMonster("wall", 3500, 1000, 0));
        GameAction action = Choose(new NormalBot(), state);
        Require(action.Type == GameActionType.SummonMonster && action.TributeInstanceIds.Count == 3,
            "consider ATK gained from original tribute ATK");
    }

    private static void VerifyThreatTarget()
    {
        GameState state = State(Monster("deck"));
        int player = state.CurrentPlayerIndex;
        Place(state, player, 0, 1001, 2000);
        Place(state, 1 - player, 0, 2001, 100);
        CardInstance threat = PlaceDefinition(state, 1 - player, 1, 2002,
            TacticalMonster("danger", 1000, 1500, 300, rank: MonsterRank.Supreme));
        Internal(state, "SetCurrentPhase", TurnPhase.Battle);
        GameAction action = Choose(new NormalBot(), state);
        Require(action.Type == GameActionType.Attack && action.TargetInstanceId == threat.InstanceId,
            "prefer a dangerous high-rank KO when neither target ends the match");
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
