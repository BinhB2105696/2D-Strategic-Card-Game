using System.Collections.Generic;

public static class GameRules
{
    public static ActionResult ApplyAction(
        GameState state,
        GameAction action)
    {
        string validationError = ValidateAction(state, action);

        if (!string.IsNullOrEmpty(validationError))
        {
            return ActionResult.Failure(validationError);
        }

        switch (action.Type)
        {
            case GameActionType.ResolveDrawStep:
                return ResolveDrawStep(state);

            case GameActionType.ResolveEnergyRefreshStep:
                return ResolveEnergyRefreshStep(state);

            case GameActionType.AdvancePhase:
                return AdvancePhase(state);

            case GameActionType.EndTurn:
                return EndTurn(state);

            case GameActionType.SummonMonster:
                return SummonMonster(state, action);

            case GameActionType.AllocateEnergy:
                return AllocateEnergy(state, action);

            case GameActionType.ChangeBattlePosition:
                return ChangeBattlePosition(state, action);

            case GameActionType.Attack:
                return Attack(state, action);

            case GameActionType.ActivateNormalSpell:
                return ActivateNormalSpell(state, action);

            default:
                return ActionResult.Failure("Unknown game action type.");
        }
    }

    public static string ValidateAction(
        GameState state,
        GameAction action)
    {
        if (state == null)
        {
            return "GameState is required.";
        }

        if (action == null)
        {
            return "GameAction is required.";
        }

        if (state.IsFinished)
        {
            return "The match has already ended.";
        }

        if (action.PlayerIndex != state.CurrentPlayerIndex)
        {
            return "Only the current player may perform this action.";
        }

        switch (action.Type)
        {
            case GameActionType.ResolveDrawStep:
                return state.CurrentPhase == TurnPhase.Recovery &&
                       state.CurrentRecoveryStep == RecoveryStep.Draw
                    ? string.Empty
                    : "Draw Step can only resolve first in Recovery Phase.";

            case GameActionType.ResolveEnergyRefreshStep:
                return state.CurrentPhase == TurnPhase.Recovery &&
                       state.CurrentRecoveryStep == RecoveryStep.EnergyRefresh
                    ? string.Empty
                    : "Energy Refresh Step can only resolve after Draw Step.";

            case GameActionType.AdvancePhase:
                return state.CurrentPhase == TurnPhase.Standby ||
                       state.CurrentPhase == TurnPhase.Main ||
                       state.CurrentPhase == TurnPhase.Battle
                    ? string.Empty
                    : "Advance Phase is only valid in Standby, Main, or Battle Phase.";

            case GameActionType.EndTurn:
                return state.CurrentPhase == TurnPhase.End
                    ? string.Empty
                    : "End Turn is only valid in End Phase.";

            case GameActionType.SummonMonster:
                return ValidateSummon(state, action);

            case GameActionType.AllocateEnergy:
                return ValidateAllocateEnergy(state, action);

            case GameActionType.ChangeBattlePosition:
                return ValidatePositionChange(state, action);

            case GameActionType.Attack:
                return ValidateAttack(state, action);

            case GameActionType.ActivateNormalSpell:
                return ValidateNormalSpell(state, action);

            default:
                return "Unknown game action type.";
        }
    }

    private static ActionResult ResolveDrawStep(GameState state)
    {
        PlayerState player = state.Players[state.CurrentPlayerIndex];
        List<GameEvent> events = new List<GameEvent>();

        if (player.ConsumeDrawStepSkip())
        {
            events.Add(GameEvent.DrawStepSkipped(player.PlayerIndex));
        }
        else
        {
            int previousHandCount = player.Hand.Count;
            player.DrawCards(state.Config.CardsDrawnPerTurn);

            for (int index = previousHandCount; index < player.Hand.Count; index++)
            {
                events.Add(GameEvent.CardDrawn(
                    player.PlayerIndex,
                    player.Hand[index].InstanceId));
            }
        }

        state.SetRecoveryStep(RecoveryStep.EnergyRefresh);
        events.Add(GameEvent.RecoveryStepChanged(
            player.PlayerIndex,
            RecoveryStep.EnergyRefresh));
        return ActionResult.Success(events);
    }

    private static ActionResult ResolveEnergyRefreshStep(GameState state)
    {
        PlayerState player = state.Players[state.CurrentPlayerIndex];
        List<GameEvent> events = new List<GameEvent>();

        if (player.ConsumeEnergyRefreshStepSkip())
        {
            events.Add(GameEvent.EnergyRefreshSkipped(player.PlayerIndex));
        }
        else
        {
            player.ResetAvailableEnergy();
            events.Add(GameEvent.EnergyRefreshed(
                player.PlayerIndex,
                player.AvailableEnergy));
        }

        if (player.ConsumeStandbyPhaseSkip())
        {
            events.Add(GameEvent.StandbyPhaseSkipped(player.PlayerIndex));
            state.SetCurrentPhase(TurnPhase.Main);
            events.Add(GameEvent.PhaseChanged(
                player.PlayerIndex,
                TurnPhase.Main));
        }
        else
        {
            state.SetCurrentPhase(TurnPhase.Standby);
            events.Add(GameEvent.PhaseChanged(
                player.PlayerIndex,
                TurnPhase.Standby));
        }

        return ActionResult.Success(events);
    }

    private static ActionResult AdvancePhase(GameState state)
    {
        TurnPhase nextPhase;

        PlayerState player = state.Players[state.CurrentPlayerIndex];

        if (state.CurrentPhase == TurnPhase.Standby)
        {
            nextPhase = TurnPhase.Main;
        }
        else if (state.CurrentPhase == TurnPhase.Main)
        {
            bool skipsBattle = state.TurnNumber == 1 ||
                player.ConsumeBattlePhaseSkip();
            nextPhase = skipsBattle ? TurnPhase.End : TurnPhase.Battle;
        }
        else
        {
            nextPhase = TurnPhase.End;
        }

        List<GameEvent> events = new List<GameEvent>();

        if (state.CurrentPhase == TurnPhase.Main &&
            nextPhase == TurnPhase.End)
        {
            events.Add(GameEvent.BattlePhaseSkipped(
                state.CurrentPlayerIndex));
        }

        state.SetCurrentPhase(nextPhase);
        events.Add(
            GameEvent.PhaseChanged(state.CurrentPlayerIndex, nextPhase));
        return ActionResult.Success(events);
    }

    private static ActionResult EndTurn(GameState state)
    {
        state.StartNextTurn();
        return ActionResult.Success(new[]
        {
            GameEvent.TurnStarted(
                state.CurrentPlayerIndex,
                state.TurnNumber),
            GameEvent.PhaseChanged(
                state.CurrentPlayerIndex,
                TurnPhase.Recovery)
        });
    }

    private static string ValidateSummon(
        GameState state,
        GameAction action)
    {
        if (state.CurrentPhase != TurnPhase.Main)
        {
            return "Monsters can only be summoned during Main Phase.";
        }

        PlayerState player = state.Players[action.PlayerIndex];
        CardInstance monster = player.FindCardInHand(action.SourceInstanceId);

        if (monster == null || !monster.Definition.IsMonster)
        {
            return "The selected Monster must be in the current player's hand.";
        }

        if (action.ZoneIndex < 0 ||
            action.ZoneIndex >= player.MonsterZones.Count)
        {
            return "The selected Monster Zone does not exist.";
        }

        if (action.BattlePosition == BattlePosition.None)
        {
            return "A summon requires Attack or Defense position.";
        }

        if (player.AvailableEnergy < state.Config.NormalSummonEnergyCost)
        {
            return "Not enough Available Energy to summon.";
        }

        int requiredTributes = monster.Definition.TributeCount;

        if (action.TributeInstanceIds.Count != requiredTributes)
        {
            return $"This Monster requires exactly {requiredTributes} tribute(s).";
        }

        HashSet<long> uniqueTributes = new HashSet<long>();

        foreach (long tributeId in action.TributeInstanceIds)
        {
            if (!uniqueTributes.Add(tributeId))
            {
                return "The same Monster cannot be used as multiple tributes.";
            }

            if (player.FindMonsterZoneIndex(tributeId) < 0)
            {
                return "Every tribute must be one of the player's field Monsters.";
            }
        }

        CardInstance destinationOccupant =
            player.MonsterZones[action.ZoneIndex];

        if (destinationOccupant != null &&
            !uniqueTributes.Contains(destinationOccupant.InstanceId))
        {
            return "The selected Monster Zone is occupied and is not being tributed.";
        }

        return string.Empty;
    }

    private static ActionResult SummonMonster(
        GameState state,
        GameAction action)
    {
        PlayerState player = state.Players[action.PlayerIndex];
        CardInstance monster = player.FindCardInHand(action.SourceInstanceId);
        List<GameEvent> events = new List<GameEvent>();

        foreach (long tributeId in action.TributeInstanceIds)
        {
            int tributeZoneIndex = player.FindMonsterZoneIndex(tributeId);
            CardInstance tribute = player.TributeMonsterAt(tributeZoneIndex);
            events.Add(GameEvent.MonsterTributed(
                player.PlayerIndex,
                tribute.InstanceId));
        }

        player.TrySpendEnergy(state.Config.NormalSummonEnergyCost);
        events.Add(GameEvent.EnergySpent(
            player.PlayerIndex,
            state.Config.NormalSummonEnergyCost));

        player.PlaceMonsterFromHand(
            monster,
            action.ZoneIndex,
            action.BattlePosition);
        events.Add(GameEvent.MonsterSummoned(
            player.PlayerIndex,
            monster.InstanceId,
            action.ZoneIndex));
        return ActionResult.Success(events);
    }

    private static string ValidateAllocateEnergy(
        GameState state,
        GameAction action)
    {
        if (state.CurrentPhase != TurnPhase.Main)
        {
            return "Energy can only be allocated during Main Phase.";
        }

        if (action.Amount <= 0)
        {
            return "Allocated Energy must be greater than zero.";
        }

        PlayerState player = state.Players[action.PlayerIndex];

        if (player.FindMonsterZoneIndex(action.SourceInstanceId) < 0)
        {
            return "Energy can only be allocated to the player's field Monster.";
        }

        return player.AvailableEnergy >= action.Amount
            ? string.Empty
            : "Not enough Available Energy to allocate.";
    }

    private static ActionResult AllocateEnergy(
        GameState state,
        GameAction action)
    {
        PlayerState player = state.Players[action.PlayerIndex];
        int zoneIndex = player.FindMonsterZoneIndex(action.SourceInstanceId);
        CardInstance monster = player.MonsterZones[zoneIndex];
        player.TrySpendEnergy(action.Amount);
        monster.AllocateEnergy(action.Amount);

        return ActionResult.Success(new[]
        {
            GameEvent.EnergySpent(player.PlayerIndex, action.Amount),
            GameEvent.EnergyAllocated(
                player.PlayerIndex,
                monster.InstanceId,
                action.Amount)
        });
    }

    private static string ValidatePositionChange(
        GameState state,
        GameAction action)
    {
        if (state.CurrentPhase != TurnPhase.Main)
        {
            return "Battle position can only change during Main Phase.";
        }

        PlayerState player = state.Players[action.PlayerIndex];
        int zoneIndex = player.FindMonsterZoneIndex(action.SourceInstanceId);

        if (zoneIndex < 0)
        {
            return "The selected Monster is not on the player's field.";
        }

        CardInstance monster = player.MonsterZones[zoneIndex];

        if (monster.WasSummonedThisTurn)
        {
            return "A Monster summoned this turn cannot change position.";
        }

        if (monster.HasChangedPositionThisTurn)
        {
            return "This Monster has already changed position this turn.";
        }

        if (action.BattlePosition == BattlePosition.None ||
            action.BattlePosition == monster.BattlePosition)
        {
            return "Choose the opposite Attack or Defense position.";
        }

        return string.Empty;
    }

    private static ActionResult ChangeBattlePosition(
        GameState state,
        GameAction action)
    {
        PlayerState player = state.Players[action.PlayerIndex];
        int zoneIndex = player.FindMonsterZoneIndex(action.SourceInstanceId);
        CardInstance monster = player.MonsterZones[zoneIndex];
        monster.SetBattlePosition(action.BattlePosition);

        return ActionResult.Success(new[]
        {
            GameEvent.BattlePositionChanged(
                player.PlayerIndex,
                monster.InstanceId,
                action.BattlePosition)
        });
    }

    private static string ValidateAttack(
        GameState state,
        GameAction action)
    {
        if (state.CurrentPhase != TurnPhase.Battle)
        {
            return "Attacks can only be declared during Battle Phase.";
        }

        if (state.TurnNumber == 1)
        {
            return "No Monster can attack during the first turn of the match.";
        }

        PlayerState attackerOwner = state.Players[action.PlayerIndex];
        int attackerZoneIndex =
            attackerOwner.FindMonsterZoneIndex(action.SourceInstanceId);

        if (attackerZoneIndex < 0)
        {
            return "The attacking Monster is not on the current player's field.";
        }

        CardInstance attacker =
            attackerOwner.MonsterZones[attackerZoneIndex];

        if (attacker.BattlePosition != BattlePosition.Attack)
        {
            return "Only a Monster in Attack Position can attack.";
        }

        if (attacker.AttacksUsedThisTurn >= attacker.MaxAttacksThisTurn)
        {
            return "This Monster has no remaining attacks this turn.";
        }

        if (attacker.AllocatedEnergy <
            attacker.Definition.AttackEnergyRequirement)
        {
            return "The Monster does not have enough allocated Energy to attack.";
        }

        PlayerState defenderOwner = state.Players[1 - action.PlayerIndex];

        if (action.TargetInstanceId == 0)
        {
            return defenderOwner.HasAnyMonster()
                ? "A direct attack is only allowed when the opponent has no Monsters."
                : string.Empty;
        }

        return defenderOwner.FindMonsterZoneIndex(action.TargetInstanceId) >= 0
            ? string.Empty
            : "The attack target is not an opponent field Monster.";
    }

    private static string ValidateNormalSpell(
        GameState state,
        GameAction action)
    {
        if (state.CurrentPhase != TurnPhase.Main)
        {
            return "A Normal Spell can only be activated during Main Phase.";
        }

        PlayerState player = state.Players[action.PlayerIndex];
        CardInstance spell = player.FindCardInHand(action.SourceInstanceId);

        if (spell == null ||
            spell.Definition.Category != CardCategory.Spell ||
            spell.Definition.SpellType != SpellType.Normal)
        {
            return "The selected Normal Spell must be in the current player's hand.";
        }

        if (spell.Definition.Effects.Count == 0)
        {
            return "This Spell has no structured effect to resolve.";
        }

        if (action.ZoneIndex < 0 ||
            action.ZoneIndex >= player.SpellTrapZones.Count)
        {
            return "The selected Spell/Trap Zone does not exist.";
        }

        if (!player.IsSpellTrapZoneOpen(action.ZoneIndex))
        {
            return "A Normal Spell requires an empty Spell/Trap Zone to activate.";
        }

        if (spell.Definition.Effects.Count != 1)
        {
            return "The current Normal Spell action requires exactly one effect.";
        }

        EffectDefinition effect = spell.Definition.Effects[0];

        if (effect.Kind != EffectKind.Activated ||
            effect.Trigger != EffectTriggerType.ManualActivation ||
            (effect.AllowedPhases & EffectPhaseMask.Main) == 0 ||
            effect.AllowedTurn != EffectTurnScope.YourTurn ||
            effect.ActivationWindow != EffectActivationWindow.OpenGameState)
        {
            return "This is not a manual Main Phase effect.";
        }

        if (!state.CanUseEffect(effect, action.PlayerIndex, spell))
        {
            return "This effect has reached its usage limit.";
        }

        if (effect.ContinuousModifiers.Count != 0)
            return "Continuous modifiers do not resolve as a Normal Spell.";

        for (int index = 0; index < effect.Conditions.Count; index++)
        {
            if (effect.Conditions[index].Type != EffectConditionType.None)
            {
                return "This Spell contains an unsupported condition.";
            }
        }

        int energyCost = 0;

        for (int index = 0; index < effect.Costs.Count; index++)
        {
            EffectCostDefinition cost = effect.Costs[index];

            if (cost.Type != EffectCostType.AvailableEnergy)
            {
                return "This Spell contains an unsupported cost.";
            }

            energyCost += cost.Amount;
        }

        if (player.AvailableEnergy < energyCost)
        {
            return "Not enough Available Energy to activate this card.";
        }

        if (effect.TargetType != EffectTargetType.None ||
            effect.TargetCount != 0 ||
            action.TargetInstanceId != 0)
        {
            return "Targeted Spell effects are not implemented yet.";
        }

        if (effect.Operations.Count == 0)
        {
            return "This Spell has no operation to resolve.";
        }

        for (int index = 0; index < effect.Operations.Count; index++)
        {
            EffectOperationDefinition operation = effect.Operations[index];

            if (operation.Type != EffectOperationType.DrawCards ||
                operation.Amount <= 0)
            {
                return "This Spell contains an unsupported or empty operation.";
            }
        }

        return string.Empty;
    }

    private static ActionResult ActivateNormalSpell(
        GameState state,
        GameAction action)
    {
        PlayerState player = state.Players[action.PlayerIndex];
        CardInstance spell = player.FindCardInHand(action.SourceInstanceId);
        EffectDefinition effect = spell.Definition.Effects[0];
        IReadOnlyList<long> targets = action.TargetInstanceId == 0
            ? new long[0]
            : new[] { action.TargetInstanceId };
        EffectContext context = new EffectContext(
            state,
            action.PlayerIndex,
            spell.InstanceId,
            targets);

        player.PlaceSpellTrapFromHand(
            spell,
            action.ZoneIndex,
            false);
        ActionResult effectResult = EffectResolver.TryResolve(
            context,
            new DefinitionEffectCondition(effect),
            new DefinitionEffectCost(effect),
            new EffectDefinitionTargetRule(effect),
            new DefinitionCardEffect(effect));

        if (!effectResult.Succeeded)
        {
            return effectResult;
        }

        state.RecordEffectUse(effect, action.PlayerIndex, spell);

        List<GameEvent> events = new List<GameEvent>
        {
            GameEvent.CardPlacedInSpellTrapZone(
                player.PlayerIndex,
                spell.InstanceId,
                action.ZoneIndex,
                state.CurrentPhase),
            GameEvent.SpellActivated(
                player.PlayerIndex,
                spell.InstanceId,
                state.CurrentPhase)
        };
        events.AddRange(effectResult.Events);
        player.MoveSpellTrapToGraveyard(action.ZoneIndex, true);
        events.Add(GameEvent.CardSentToGraveyard(
            player.PlayerIndex,
            spell.InstanceId,
            state.CurrentPhase));
        return ActionResult.Success(events);
    }

    private static ActionResult Attack(
        GameState state,
        GameAction action)
    {
        PlayerState attackerOwner = state.Players[action.PlayerIndex];
        PlayerState defenderOwner = state.Players[1 - action.PlayerIndex];
        int attackerZoneIndex =
            attackerOwner.FindMonsterZoneIndex(action.SourceInstanceId);
        CardInstance attacker =
            attackerOwner.MonsterZones[attackerZoneIndex];
        List<GameEvent> events = new List<GameEvent>
        {
            GameEvent.AttackDeclared(
                attackerOwner.PlayerIndex,
                attacker.InstanceId,
                action.TargetInstanceId)
        };

        attacker.MarkAttacked();

        if (action.TargetInstanceId == 0)
        {
            defenderOwner.TakeDamage(attacker.CurrentAttack);
            events.Add(GameEvent.PlayerDamaged(
                defenderOwner.PlayerIndex,
                attacker.CurrentAttack));
            AddMatchEndIfNeeded(state, attackerOwner, defenderOwner, events);
            return ActionResult.Success(events);
        }

        int defenderZoneIndex =
            defenderOwner.FindMonsterZoneIndex(action.TargetInstanceId);
        CardInstance defender =
            defenderOwner.MonsterZones[defenderZoneIndex];
        int damage = defender.BattlePosition == BattlePosition.Defense
            ? System.Math.Max(
                0,
                attacker.CurrentAttack - defender.CurrentDefense)
            : attacker.CurrentAttack;

        defender.ReceiveDamage(damage);
        events.Add(GameEvent.MonsterDamaged(
            defenderOwner.PlayerIndex,
            defender.InstanceId,
            damage));

        if (defender.IsDestroyed)
        {
            int destructionPenalty = defender.AllocatedEnergy *
                state.Config.DestructionPenaltyPerEnergy;
            defenderOwner.MoveMonsterToGraveyard(defenderZoneIndex);
            events.Add(GameEvent.MonsterDestroyed(
                defenderOwner.PlayerIndex,
                defender.InstanceId));

            if (destructionPenalty > 0)
            {
                defenderOwner.TakeDamage(destructionPenalty);
                events.Add(GameEvent.PlayerDamaged(
                    defenderOwner.PlayerIndex,
                    destructionPenalty));
            }
        }

        AddMatchEndIfNeeded(state, attackerOwner, defenderOwner, events);
        return ActionResult.Success(events);
    }

    private static void AddMatchEndIfNeeded(
        GameState state,
        PlayerState attackerOwner,
        PlayerState defenderOwner,
        List<GameEvent> events)
    {
        if (!defenderOwner.HasLost)
        {
            return;
        }

        state.EndMatch(attackerOwner.PlayerIndex);
        events.Add(GameEvent.MatchEnded(attackerOwner.PlayerIndex));
    }
}
