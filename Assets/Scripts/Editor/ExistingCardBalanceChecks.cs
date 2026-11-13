using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// Pure rules regression; also called by the explicit Editor asset updater.
public static class ExistingCardBalanceChecks
{
    private static int checks;

    public static int Run()
    {
        checks = 0;
        Growth();
        NameLimits();
        FreeStrike();
        FirstTurnLock();
        TemporaryProtection();
        FighterChoice(false, false);
        FighterChoice(true, false);
        FighterChoice(false, true);
        FieldChoice(false, false);
        FieldChoice(true, false);
        FieldChoice(false, true);
        return checks;
    }

#if !UNITY_EDITOR
    public static void Main() => Console.WriteLine("Existing card balance PASS: " + Run() + " checks.");
#endif

    private static EffectDefinition Effect(string id, EffectTriggerType trigger, EffectOperationType operation,
        int amount = 0, int energy = 0, bool nameLimit = false, EffectTargetType target = EffectTargetType.None,
        EffectConditionType condition = EffectConditionType.None)
        => new EffectDefinition(id, trigger, EffectKind.Activated,
            trigger == EffectTriggerType.EndPhase ? EffectPhaseMask.End : EffectPhaseMask.Main,
            EffectTurnScope.YourTurn, EffectActivationWindow.OpenGameState, EffectActivationSource.Standard,
            condition == EffectConditionType.None ? null : new[] { new EffectConditionDefinition(condition, 0) },
            energy == 0 ? null : new[] { new EffectCostDefinition(EffectCostType.AvailableEnergy, energy) },
            target, target == EffectTargetType.None ? 0 : 1,
            operation == EffectOperationType.None ? null : new[] { new EffectOperationDefinition(operation, amount) },
            1, nameLimit ? EffectUsageScope.PerCardId : EffectUsageScope.PerInstance);

    private static CardDefinition Monster(string id, int hp = 1000, int attack = 400, int defense = 300,
        EffectDefinition effect = null, MonsterRank rank = MonsterRank.Basic)
        => new CardDefinition(id, id, CardCategory.Monster, "", MonsterAttribute.Wood, rank,
            MonsterType.Warrior, hp, attack, defense, 1, SpellType.Normal, TrapType.Normal, (int)rank,
            CardSpeed.Speed1, effect == null ? null : new[] { effect });

    private static CardDefinition Spell(string id, EffectDefinition effect)
        => new CardDefinition(id, id, CardCategory.Spell, "", MonsterAttribute.Wood, MonsterRank.Basic,
            MonsterType.Warrior, 0, 0, 0, 0, SpellType.Normal, TrapType.Normal, 0, CardSpeed.Speed1, new[] { effect });

    private static GameState State(CardDefinition filler = null, bool firstTurn = false)
    {
        filler = filler ?? Monster("balance.filler");
        var deck = Enumerable.Repeat(filler, 30).ToArray();
        GameState state = GameState.CreateNewMatch(GameConfig.CreateDefault(), 57, deck, deck);
        if (!firstTurn) Call(state, "StartNextTurn");
        Call(state, "SetCurrentPhase", TurnPhase.Main);
        return state;
    }

    private static CardInstance Place(GameState state, int player, int zone, long id, CardDefinition definition,
        BattlePosition position = BattlePosition.Attack, int energy = 0)
    {
        var card = new CardInstance(id, player, definition);
        Call(state.Players[player], "PlaceMonsterFromHand", card, zone, position);
        card.AllocateEnergy(energy);
        card.ResetTurnFlags();
        return card;
    }

    private static void Growth()
    {
        var tide = Monster("tide", 600, effect: Effect("tide.grow", EffectTriggerType.EndPhase,
            EffectOperationType.IncreaseMaximumHealth, 200, condition: EffectConditionType.SourceInDefense));
        var stone = Monster("stone", effect: Effect("stone.grow", EffectTriggerType.EndPhase,
            EffectOperationType.IncreaseDefensePermanently, 100, condition: EffectConditionType.SourceInDefense));
        GameState state = State(); int actor = state.CurrentPlayerIndex;
        CardInstance defender = Place(state, actor, 0, 1001, tide, BattlePosition.Defense);
        defender.ReceiveDamage(200);
        CardInstance wall = Place(state, actor, 1, 1002, stone, BattlePosition.Defense);
        CardInstance attacker = Place(state, actor, 2, 1003, tide);
        CardInstance opponent = Place(state, 1 - actor, 0, 2001, tide, BattlePosition.Defense);
        Apply(state, GameAction.EnterEndPhase(actor));
        Check(defender.MaximumHealth == 800 && defender.CurrentHealth == 600, "Tide grows and preserves damage");
        Check(wall.CurrentDefense == 400 && state.Players[actor].AvailableEnergy == 5, "free Stone growth");
        Check(attacker.MaximumHealth == 600 && opponent.MaximumHealth == 600, "only own Defense monsters grow");
        Check(!GameRules.ApplyAction(state, GameAction.EnterEndPhase(actor)).Succeeded, "cannot repeat End entry");
        Apply(state, GameAction.EndTurn(actor));
        Check(defender.MaximumHealth == 800 && wall.CurrentDefense == 400, "EndTurn does not repeat growth");
        Call(state, "StartNextTurn"); Call(state, "SetCurrentPhase", TurnPhase.Battle);
        Apply(state, GameAction.AdvancePhase(actor));
        Check(defender.MaximumHealth == 1000 && wall.CurrentDefense == 500, "AdvancePhase End path grows once next own turn");
    }

    private static void NameLimits()
    {
        CardDefinition draw = Spell("insight", Effect("insight.draw", EffectTriggerType.ManualActivation,
            EffectOperationType.DrawCards, 2, 1, true));
        GameState state = State(draw); int actor = state.CurrentPlayerIndex;
        PlayerState owner = state.Players[actor]; int cards = owner.Hand.Count, deck = owner.Deck.Count;
        Apply(state, GameAction.ActivateCard(actor, owner.Hand[0].InstanceId, 0));
        Check(owner.Hand.Count == cards + 1 && owner.Deck.Count == deck - 2 && owner.AvailableEnergy == 4,
            "Insight spends one card and one Energy to draw two");
        Check(!GameRules.ApplyAction(state, GameAction.ActivateCard(actor, owner.Hand[0].InstanceId, 0)).Succeeded,
            "second copy of same name is blocked");
        Check(owner.AvailableEnergy == 4 && owner.Hand.Count == cards + 1, "invalid use pays nothing");
        Call(state, "StartNextTurn"); Call(state, "SetCurrentPhase", TurnPhase.Main);
        int other = state.CurrentPlayerIndex;
        Apply(state, GameAction.ActivateCard(other, state.Players[other].Hand[0].InstanceId, 0));
        Check(state.Players[other].AvailableEnergy == 4, "name limit belongs to each player");
        Call(state, "StartNextTurn"); Call(state, "SetCurrentPhase", TurnPhase.Main);
        Apply(state, GameAction.ActivateCard(actor, owner.Hand[0].InstanceId, 0));
        Check(owner.AvailableEnergy == 3, "name limit resets next own turn");
    }

    private static void FreeStrike()
    {
        GameState state = State(Spell("strike", Effect("strike.buff", EffectTriggerType.ManualActivation,
            EffectOperationType.IncreaseAttackUntilEndOfTurn, 300, nameLimit: true, target: EffectTargetType.OwnMonster)));
        int actor = state.CurrentPlayerIndex; PlayerState owner = state.Players[actor];
        CardInstance target = Place(state, actor, 0, 1001, Monster("target"));
        owner.TrySpendEnergy(5);
        Apply(state, GameAction.ActivateCard(actor, owner.Hand[0].InstanceId, 0, target.InstanceId));
        Check(target.CurrentAttack == 700 && owner.AvailableEnergy == 0, "Quick Strike works with zero Energy");
        Check(!GameRules.ApplyAction(state, GameAction.ActivateCard(actor, owner.Hand[0].InstanceId, 0, target.InstanceId)).Succeeded,
            "free buff still respects name limit");
        Apply(state, GameAction.EnterEndPhase(actor)); Apply(state, GameAction.EndTurn(actor));
        Check(target.CurrentAttack == 400, "Quick Strike expires at turn end");
    }

    private static void FirstTurnLock()
    {
        var effect = new EffectDefinition("lock.skip", EffectTriggerType.ManualActivation, EffectKind.Activated,
            EffectPhaseMask.Main, EffectTurnScope.YourTurn, EffectActivationWindow.OpenGameState,
            EffectActivationSource.Standard, new[] { new EffectConditionDefinition(EffectConditionType.AfterFirstTurn, 0) },
            null, EffectTargetType.None, 0, new[] { new EffectOperationDefinition(EffectOperationType.SkipNextOpponentDrawStep, 1),
                new EffectOperationDefinition(EffectOperationType.SkipCurrentBattlePhase, 1) }, 1, EffectUsageScope.PerCardId);
        GameState state = State(Spell("lock", effect), true); int actor = state.CurrentPlayerIndex;
        int hand = state.Players[actor].Hand.Count;
        Check(!GameRules.ApplyAction(state, GameAction.ActivateCard(actor, state.Players[actor].Hand[0].InstanceId, 0)).Succeeded,
            "Temporal Lock is rejected on turn one");
        Check(state.Players[actor].Hand.Count == hand && state.Players[actor].AvailableEnergy == 5,
            "rejected first-turn Lock leaves resources intact");
        Call(state, "StartNextTurn"); Call(state, "SetCurrentPhase", TurnPhase.Main); actor = state.CurrentPlayerIndex;
        Apply(state, GameAction.ActivateCard(actor, state.Players[actor].Hand[0].InstanceId, 0));
        Apply(state, GameAction.AdvancePhase(actor));
        Check(state.CurrentPhase == TurnPhase.End, "later Lock skips its owner's Battle");
    }

    private static void TemporaryProtection()
    {
        GameState state = State(); int actor = state.CurrentPlayerIndex;
        CardInstance standing = Place(state, actor, 0, 1001, Monster("standing"), energy: 2);
        CardInstance defending = Place(state, actor, 1, 1002, Monster("defending"), BattlePosition.Defense, 2);
        EffectDefinition protection = Effect("guard", EffectTriggerType.ManualActivation,
            EffectOperationType.ProtectAttackedMonster, 500, target: EffectTargetType.OwnMonster);
        foreach (CardInstance target in new[] { standing, defending })
            new DefinitionCardEffect(protection).Resolve(new EffectContext(state, actor, target.InstanceId,
                new[] { target.InstanceId }), new List<GameEvent>());
        Check(standing.CurrentDefense == 800 && standing.BattlePosition == BattlePosition.Defense && standing.AllocatedEnergy == 0,
            "Guardian changes Attack to Defense and removes its Energy");
        Check(defending.CurrentDefense == 800 && defending.AllocatedEnergy == 2,
            "already-Defense target keeps its Energy");
        Apply(state, GameAction.EnterEndPhase(actor)); Apply(state, GameAction.EndTurn(actor));
        Check(standing.CurrentDefense == 300 && defending.CurrentDefense == 300, "Guardian DEF expires this turn");
        Check(standing.BattlePosition == BattlePosition.Defense, "Guardian does not revert position on expiry");
    }

    private static void FighterChoice(bool accept, bool fullHealth)
    {
        GameState state = State(); int actor = state.CurrentPlayerIndex;
        CardInstance fighter = Place(state, actor, 0, 1001, Monster("fighter", 3500, 1500, 800,
            Effect("fighter.heal", EffectTriggerType.OnBattleDestroyedMonster,
                EffectOperationType.RestoreHealthPerRemovedAllocatedEnergy, 500), MonsterRank.Ultimate), energy: 2);
        if (!fullHealth) fighter.ReceiveDamage(500);
        CardInstance imp = Place(state, 1 - actor, 0, 2001, Monster("imp", 500, effect:
            Effect("imp.recover", EffectTriggerType.OnDestroyed, EffectOperationType.RecoverBasicMonsterFromGraveyard, 1)));
        Call(state, "SetCurrentPhase", TurnPhase.Battle);
        Apply(state, GameAction.Attack(actor, fighter.InstanceId, imp.InstanceId));
        if (fullHealth)
        {
            Check(fighter.AllocatedEnergy == 2 && state.PendingTriggeredEffect.SourceInstanceId == imp.InstanceId,
                "full-HP Fighter preserves Energy without a useless prompt");
        }
        else
        {
            Check(state.PendingTriggeredEffect.SourceInstanceId == fighter.InstanceId && fighter.AllocatedEnergy == 2,
                "Fighter waits for choice before removing Energy");
            Apply(state, accept ? GameAction.ResolveTriggeredEffect(actor, fighter.InstanceId, fighter.InstanceId)
                : GameAction.DeclineTriggeredEffect(actor, fighter.InstanceId));
            Check(fighter.CurrentHealth == (accept ? 3500 : 3000) && fighter.AllocatedEnergy == (accept ? 0 : 2),
                accept ? "accepted recovery heals to cap and spends all Energy" : "declined recovery preserves HP and Energy");
            Check(state.PendingTriggeredEffect != null && state.PendingTriggeredEffect.SourceInstanceId == imp.InstanceId,
                "victim Imp recovery is queued instead of lost");
        }
        Apply(state, GameAction.ResolveTriggeredEffect(1 - actor, imp.InstanceId, imp.InstanceId));
        Check(imp.Zone == CardZone.Hand && !state.HasPendingTriggeredEffect, "queued opponent effect resolves normally");
    }

    private sealed class FieldBuff : IContinuousModifier
    {
        public int ModifyHealth(CardInstance source, CardInstance target) => 0;
        public int ModifyAttack(CardInstance source, CardInstance target) => 200;
        public int ModifyDefense(CardInstance source, CardInstance target) => 200;
    }

    private static void FieldChoice(bool accept, bool empty)
    {
        var buff = new EffectDefinition("field.buff", EffectTriggerType.ManualActivation, EffectKind.Continuous,
            EffectPhaseMask.Main, EffectTurnScope.YourTurn, EffectActivationWindow.OpenGameState,
            EffectActivationSource.Standard, null, null, EffectTargetType.None, 0, null,
            continuousModifiers: new[] { new FieldBuff() });
        EffectDefinition upkeep = Effect("field.upkeep", EffectTriggerType.EndPhase, EffectOperationType.None, energy: 1);
        var field = new CardDefinition("field", "field", CardCategory.Spell, "", MonsterAttribute.Wood, MonsterRank.Basic,
            MonsterType.Warrior, 0, 0, 0, 0, SpellType.Field, TrapType.Normal, 0, CardSpeed.Speed1, new[] { buff, upkeep });
        GameState state = State(field); int actor = state.CurrentPlayerIndex; PlayerState owner = state.Players[actor];
        CardInstance target = Place(state, actor, 0, 1001, Monster("field.target"));
        long fieldId = owner.Hand[0].InstanceId;
        Apply(state, GameAction.ActivateCard(actor, fieldId));
        Check(target.CurrentAttack == 600, "Field modifier is active");
        if (empty) owner.TrySpendEnergy(5);
        Apply(state, GameAction.EnterEndPhase(actor));
        if (empty)
            Check(owner.FieldSpell == null && !state.HasPendingTriggeredEffect && target.CurrentAttack == 400,
                "unaffordable Field leaves and removes its modifiers");
        else
        {
            Check(state.PendingTriggeredEffect.SourceInstanceId == fieldId && owner.AvailableEnergy == 5,
                "Field offers upkeep without automatically charging");
            Apply(state, accept ? GameAction.ResolveTriggeredEffect(actor, fieldId, fieldId)
                : GameAction.DeclineTriggeredEffect(actor, fieldId));
            Check(owner.AvailableEnergy == (accept ? 4 : 5) && (owner.FieldSpell != null) == accept,
                accept ? "accepted upkeep costs one Energy" : "declining Field preserves Energy");
            Check(target.CurrentAttack == (accept ? 600 : 400), "Field choice keeps or removes modifiers");
        }
        Apply(state, GameAction.EndTurn(actor));
        Check(state.CurrentPlayerIndex != actor && !state.HasPendingTriggeredEffect, "upkeep is not repeated by EndTurn");
    }

    private static void Apply(GameState state, GameAction action)
    {
        ActionResult result = GameRules.ApplyAction(state, action);
        if (!result.Succeeded) throw new InvalidOperationException(action + ": " + result.ErrorMessage);
    }

    private static void Call(object instance, string method, params object[] args)
        => instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Existing balance FAIL: " + message);
        checks++;
    }
}
