using System.Collections.Generic;

public static class GameRules
{
    public static IReadOnlyList<GameAction> GetLegalActions(
        GameState state,
        int playerIndex)
    {
        return LegalActionEnumerator.GetLegalActions(state, playerIndex);
    }

    public static IReadOnlyList<GameAction> GetLegalResponseActions(
        GameState state,
        int playerIndex)
    {
        List<GameAction> actions = new List<GameAction>();
        if (state == null ||
            !state.HasOpenResponseWindow ||
            state.InteractionPlayerIndex != playerIndex)
        {
            return actions;
        }

        PlayerState player = state.Players[playerIndex];
        for (int zoneIndex = 0;
             zoneIndex < player.SpellTrapZones.Count;
             zoneIndex++)
        {
            CardInstance card = player.SpellTrapZones[zoneIndex];
            if (card == null || card.Definition.Category != CardCategory.Trap)
                continue;

            EffectDefinition effect = FindActivationEffect(
                card,
                state.ActiveChain);
            if (effect == null || effect.TargetCount > 1)
                continue;

            if (effect.TargetCount == 0)
            {
                AddResponseIfLegal(card.InstanceId, 0);
                continue;
            }

            if (state.ActiveChain.Links.Count == 0 &&
                state.ActiveChain.PendingAttack != null)
            {
                long responseTarget = effect.TargetType ==
                    EffectTargetType.OwnMonster
                    ? state.ActiveChain.PendingAttack.TargetInstanceId
                    : state.ActiveChain.PendingAttack.AttackerInstanceId;
                if (responseTarget == 0)
                    continue;
                AddResponseIfLegal(
                    card.InstanceId,
                    responseTarget);
                continue;
            }

            int targetOwnerIndex = effect.TargetType ==
                EffectTargetType.OwnMonster
                ? playerIndex
                : 1 - playerIndex;
            PlayerState targetOwner = state.Players[targetOwnerIndex];
            for (int targetZone = 0;
                 targetZone < targetOwner.MonsterZones.Count;
                 targetZone++)
            {
                CardInstance target = targetOwner.MonsterZones[targetZone];
                if (target != null)
                    AddResponseIfLegal(card.InstanceId, target.InstanceId);
            }
        }

        return actions;

        void AddResponseIfLegal(long sourceId, long targetId)
        {
            GameAction action = GameAction.ActivateCard(
                playerIndex,
                sourceId,
                targetInstanceId: targetId);
            if (string.IsNullOrEmpty(ValidateAction(state, action)))
                actions.Add(action);
        }
    }

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

            case GameActionType.EnterEndPhase:
                return EnterEndPhase(state);

            case GameActionType.SummonMonster:
                return SummonMonster(state, action);

            case GameActionType.AllocateEnergy:
                return AllocateEnergy(state, action);

            case GameActionType.ChangeBattlePosition:
                return ChangeBattlePosition(state, action);

            case GameActionType.Attack:
                return Attack(state, action);

            case GameActionType.ActivateNormalSpell:
            case GameActionType.ActivateCard:
                return ActivateCard(state, action);

            case GameActionType.SetTrap:
                return SetTrap(state, action);

            case GameActionType.PassResponse:
                return PassResponse(state, action);

            case GameActionType.ResolveTriggeredEffect:
                return ResolveTriggeredEffect(state, action);

            case GameActionType.DeclineTriggeredEffect:
                return DeclineTriggeredEffect(state, action);

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

        int expectedPlayer = state.InteractionPlayerIndex;
        if (action.PlayerIndex != expectedPlayer)
        {
            return state.HasOpenResponseWindow
                ? "Only the player with response priority may act."
                : "Only the current player may perform this action.";
        }

        if (state.HasOpenResponseWindow &&
            action.Type != GameActionType.ActivateCard &&
            action.Type != GameActionType.PassResponse)
        {
            return "Only a legal response or Pass is allowed while a response window is open.";
        }

        if (state.HasPendingTriggeredEffect &&
            action.Type != GameActionType.ResolveTriggeredEffect &&
            action.Type != GameActionType.DeclineTriggeredEffect)
        {
            return "Resolve or decline the pending triggered effect first.";
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

            case GameActionType.EnterEndPhase:
                return state.CurrentPhase == TurnPhase.Main ||
                       state.CurrentPhase == TurnPhase.Battle
                    ? string.Empty
                    : "End Phase can only be entered from Main or Battle Phase.";

            case GameActionType.SummonMonster:
                return ValidateSummon(state, action);

            case GameActionType.AllocateEnergy:
                return ValidateAllocateEnergy(state, action);

            case GameActionType.ChangeBattlePosition:
                return ValidatePositionChange(state, action);

            case GameActionType.Attack:
                return ValidateAttack(state, action);

            case GameActionType.ActivateNormalSpell:
                return ValidateCardActivation(state, action, true);

            case GameActionType.ActivateCard:
                return ValidateCardActivation(state, action, false);

            case GameActionType.SetTrap:
                return ValidateSetTrap(state, action);

            case GameActionType.PassResponse:
                return state.HasOpenResponseWindow
                    ? string.Empty
                    : "There is no response window to pass.";

            case GameActionType.ResolveTriggeredEffect:
                return ValidateTriggeredEffectSelection(state, action);

            case GameActionType.DeclineTriggeredEffect:
                return ValidateTriggeredEffectDecline(state, action);

            default:
                return "Unknown game action type.";
        }
    }

    private static ActionResult ResolveDrawStep(GameState state)
    {
        PlayerState player = state.Players[state.CurrentPlayerIndex];
        List<GameEvent> events = new List<GameEvent>();

        if (player.ConsumeDrawStepSkip(state.TurnNumber))
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

        if (player.ConsumeEnergyRefreshStepSkip(state.TurnNumber))
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

        if (player.ConsumeStandbyPhaseSkip(state.TurnNumber))
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
            bool effectSkipsBattle =
                player.ConsumeBattlePhaseSkip(state.TurnNumber);
            bool skipsBattle = state.TurnNumber == 1 || effectSkipsBattle;
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
        int completedTurn = state.TurnNumber;
        int endingPlayerIndex = state.CurrentPlayerIndex;
        List<GameEvent> events = new List<GameEvent>();
        ResolveFieldMaintenance(state, endingPlayerIndex, events);
        state.ExpireTemporaryModifiers(completedTurn);
        state.StartNextTurn();
        events.Add(GameEvent.TurnStarted(
            state.CurrentPlayerIndex,
            state.TurnNumber));
        events.Add(GameEvent.PhaseChanged(
            state.CurrentPlayerIndex,
            TurnPhase.Recovery));
        return ActionResult.Success(events);
    }

    private static ActionResult EnterEndPhase(GameState state)
    {
        int playerIndex = state.CurrentPlayerIndex;
        List<GameEvent> events = new List<GameEvent>();

        if (state.CurrentPhase == TurnPhase.Main)
        {
            // Leaving Main completes this turn's Battle-skip consequence,
            // including on turn 1 when Battle is unavailable by default.
            state.Players[playerIndex].ConsumeBattlePhaseSkip(state.TurnNumber);

            events.Add(GameEvent.BattlePhaseSkipped(playerIndex));
        }

        state.SetCurrentPhase(TurnPhase.End);
        events.Add(GameEvent.PhaseChanged(playerIndex, TurnPhase.End));
        return ActionResult.Success(events);
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

        int tributeBaseAttack = 0;
        foreach (long tributeId in action.TributeInstanceIds)
        {
            int tributeZoneIndex = player.FindMonsterZoneIndex(tributeId);
            tributeBaseAttack += player.MonsterZones[tributeZoneIndex]
                .Definition.BaseAttack;
            CardInstance tribute = SendMonsterToGraveyard(
                state,
                player,
                tributeZoneIndex,
                events);
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
        ApplyFieldModifiersToMonster(state, player, monster, events);
        events.Add(GameEvent.MonsterSummoned(
            player.PlayerIndex,
            monster.InstanceId,
            action.ZoneIndex));
        ResolveTriggeredEffects(
            state,
            monster,
            EffectTriggerType.OnSummoned,
            events,
            tributeBaseAttack);
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
        List<GameEvent> events = new List<GameEvent>
        {
            GameEvent.EnergySpent(player.PlayerIndex, action.Amount),
            GameEvent.EnergyAllocated(
                player.PlayerIndex,
                monster.InstanceId,
                action.Amount)
        };
        RefreshEquipModifiersForTarget(state, monster, events);
        return ActionResult.Success(events);
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

    private static string ValidateSetTrap(GameState state, GameAction action)
    {
        if (state.HasOpenResponseWindow)
            return "A Trap cannot be Set while a response window is open.";
        if (state.CurrentPhase != TurnPhase.Main)
            return "A Trap can only be Set during Main Phase.";

        PlayerState player = state.Players[action.PlayerIndex];
        CardInstance trap = player.FindCardInHand(action.SourceInstanceId);
        if (trap == null || trap.Definition.Category != CardCategory.Trap)
            return "The selected Trap must be in the current player's hand.";
        if (!player.IsSpellTrapZoneOpen(action.ZoneIndex))
            return "A Trap requires an empty Spell/Trap Zone to be Set.";
        return string.Empty;
    }

    private static ActionResult SetTrap(GameState state, GameAction action)
    {
        PlayerState player = state.Players[action.PlayerIndex];
        CardInstance trap = player.FindCardInHand(action.SourceInstanceId);
        player.SetTrapFromHand(trap, action.ZoneIndex, state.TurnNumber);
        return ActionResult.Success(new[]
        {
            GameEvent.CardPlacedInSpellTrapZone(
                player.PlayerIndex,
                trap.InstanceId,
                action.ZoneIndex,
                state.CurrentPhase),
            GameEvent.CardSet(
                player.PlayerIndex,
                trap.InstanceId,
                action.ZoneIndex)
        });
    }

    private static string ValidateCardActivation(
        GameState state,
        GameAction action,
        bool requireLegacyNormalSpell)
    {
        PlayerState player = state.Players[action.PlayerIndex];
        CardInstance card = player.FindCard(action.SourceInstanceId);
        if (card == null)
            return "The activating card does not belong to this player.";

        if (requireLegacyNormalSpell &&
            (card.Definition.Category != CardCategory.Spell ||
             card.Definition.SpellType != SpellType.Normal))
        {
            return "The selected card must be a Normal Spell.";
        }

        EffectDefinition effect = FindActivationEffect(card, state.ActiveChain);
        if (effect == null)
            return "This card has no effect for the current activation window.";
        if (effect.ActivationSource != EffectActivationSource.Standard)
            return "This special activation source is not implemented yet.";
        if ((effect.AllowedPhases & PhaseMask(state.CurrentPhase)) == 0)
            return "This effect cannot be activated in the current phase.";
        if (!IsAllowedTurn(effect.AllowedTurn, state, action.PlayerIndex))
            return "This effect cannot be activated during this player's turn.";
        if (!state.CanUseEffect(effect, action.PlayerIndex, card))
            return "This effect has reached its usage limit.";

        string sourceError = ValidateStandardActivationSource(
            state,
            action,
            player,
            card);
        if (!string.IsNullOrEmpty(sourceError))
            return sourceError;

        string responseError = ValidateResponseTiming(
            state,
            action,
            card,
            effect);
        if (!string.IsNullOrEmpty(responseError))
            return responseError;

        IReadOnlyList<long> targets = ActionTargets(action);
        EffectContext context = new EffectContext(
            state,
            action.PlayerIndex,
            card.InstanceId,
            targets);
        string failureReason;
        if (!new DefinitionEffectCondition(effect).IsMet(
                context,
                out failureReason))
            return failureReason;
        if (!new DefinitionEffectCost(effect).CanPay(
                context,
                out failureReason))
            return failureReason;
        if (!new EffectDefinitionTargetRule(effect).AreTargetsValid(
                context,
                out failureReason))
            return failureReason;

        if (state.ActiveChain != null &&
            state.ActiveChain.WindowType == ResponseWindowType.AttackDeclaration &&
            effect.ActivationWindow ==
                EffectActivationWindow.ResponseToAttackDeclaration &&
            !IsLegalAttackResponseTarget(
                state,
                action.PlayerIndex,
                effect,
                targets))
        {
            return "This Trap must target the correct Monster in the pending attack.";
        }

        return string.Empty;
    }

    private static string ValidateStandardActivationSource(
        GameState state,
        GameAction action,
        PlayerState player,
        CardInstance card)
    {
        if (!state.HasOpenResponseWindow)
        {
            if (card.Definition.Category == CardCategory.Monster &&
                card.Zone == CardZone.MonsterZone &&
                state.CurrentPhase == TurnPhase.Main)
            {
                return string.Empty;
            }

            if (card.Definition.Category != CardCategory.Spell ||
                card.Zone != CardZone.Hand ||
                state.CurrentPhase != TurnPhase.Main)
            {
                return "Only a Spell in hand can be activated in an open game state.";
            }

            if (card.Definition.SpellType == SpellType.Field)
            {
                return player.LastFieldSpellActivationTurn == state.TurnNumber
                    ? "Only one Field Spell may be activated per player per turn."
                    : string.Empty;
            }

            if (!player.IsSpellTrapZoneOpen(action.ZoneIndex))
                return "This Spell requires an empty Spell/Trap Zone.";
            return string.Empty;
        }

        int zoneIndex = player.FindSpellTrapZoneIndex(card.InstanceId);
        if (card.Definition.Category != CardCategory.Trap ||
            zoneIndex < 0 ||
            !card.IsFaceDown)
        {
            return "A standard Trap response must be Set face-down on the field.";
        }
        if (card.SetTurnNumber >= state.TurnNumber)
            return "A Trap cannot be activated during the turn it was Set.";
        return string.Empty;
    }

    private static string ValidateResponseTiming(
        GameState state,
        GameAction action,
        CardInstance card,
        EffectDefinition effect)
    {
        ChainState chain = state.ActiveChain;
        if (chain == null)
        {
            return effect.ActivationWindow == EffectActivationWindow.OpenGameState
                ? string.Empty
                : "This effect requires a response window.";
        }

        if (effect.ActivationWindow == EffectActivationWindow.OpenGameState)
            return "Speed 1 effects cannot be activated as a response.";
        bool respondsToExistingLink = chain.Links.Count > 0;
        if (respondsToExistingLink &&
            effect.ActivationWindow != EffectActivationWindow.ResponseToActivation)
        {
            return "This effect does not respond to card activation.";
        }
        if (!respondsToExistingLink &&
            chain.WindowType == ResponseWindowType.AttackDeclaration &&
            effect.ActivationWindow !=
                EffectActivationWindow.ResponseToAttackDeclaration)
        {
            return "This effect does not respond to an attack declaration.";
        }
        if (!respondsToExistingLink &&
            chain.WindowType == ResponseWindowType.CardActivation &&
            effect.ActivationWindow != EffectActivationWindow.ResponseToActivation)
        {
            return "This effect does not respond to card activation.";
        }
        if (card.Definition.Speed == CardSpeed.Speed1)
            return "Speed 1 cards cannot be added as responses.";
        if (chain.Links.Count > 0)
        {
            ChainLink previous = chain.Links[chain.Links.Count - 1];
            if (effect.Trigger == EffectTriggerType.OnSpellTrapActivated)
            {
                CardInstance previousSource = state.FindCard(
                    previous.SourceInstanceId);
                if (previousSource == null ||
                    (previousSource.Definition.Category != CardCategory.Spell &&
                     previousSource.Definition.Category != CardCategory.Trap))
                {
                    return "This effect only responds to a Spell or Trap activation.";
                }
            }
            if (previous.Speed == CardSpeed.Speed3 &&
                card.Definition.Speed != CardSpeed.Speed3)
            {
                return "Only a Speed 3 card can respond to Speed 3.";
            }
            if (ContainsOperation(
                    effect,
                    EffectOperationType.NegatePreviousChainLink) &&
                previous.PlayerIndex == action.PlayerIndex)
            {
                return "This Counter Trap must negate an opponent's activation.";
            }
        }
        return string.Empty;
    }

    private static ActionResult ActivateCard(
        GameState state,
        GameAction action)
    {
        PlayerState player = state.Players[action.PlayerIndex];
        CardInstance card = player.FindCard(action.SourceInstanceId);
        EffectDefinition effect = FindActivationEffect(card, state.ActiveChain);
        IReadOnlyList<long> targets = ActionTargets(action);
        EffectContext context = new EffectContext(
            state,
            action.PlayerIndex,
            card.InstanceId,
            targets);
        List<GameEvent> events = new List<GameEvent>();

        if (card.Zone == CardZone.Hand)
        {
            if (card.Definition.SpellType == SpellType.Field)
            {
                CardInstance replaced = player.FieldSpell;
                if (replaced != null)
                {
                    state.RemovePersistentModifiersFromSource(
                        replaced.InstanceId);
                    events.Add(GameEvent.CardSentToGraveyard(
                        player.PlayerIndex,
                        replaced.InstanceId,
                        state.CurrentPhase));
                }
                player.PlaceFieldSpellFromHand(card);
                player.MarkFieldSpellActivated(state.TurnNumber);
                events.Add(GameEvent.FieldSpellActivated(
                    player.PlayerIndex,
                    card.InstanceId));
            }
            else
            {
                player.PlaceSpellTrapFromHand(card, action.ZoneIndex, false);
                events.Add(GameEvent.CardPlacedInSpellTrapZone(
                    player.PlayerIndex,
                    card.InstanceId,
                    action.ZoneIndex,
                    state.CurrentPhase));
            }
        }
        else
        {
            card.Reveal();
        }

        new DefinitionEffectCost(effect).Pay(context, events);
        state.RecordEffectUse(effect, action.PlayerIndex, card);

        int sourceZoneIndex = player.FindSpellTrapZoneIndex(card.InstanceId);
        ChainLink link = new ChainLink(
            action.PlayerIndex,
            card.InstanceId,
            sourceZoneIndex,
            card.Definition.Speed,
            effect,
            targets);
        bool startsChain = state.ActiveChain == null;
        if (startsChain)
        {
            state.OpenChain(new ChainState(
                ResponseWindowType.CardActivation,
                1 - action.PlayerIndex));
        }
        state.ActiveChain.AddLink(link);

        events.Add(card.Definition.Category == CardCategory.Trap
            ? GameEvent.TrapActivated(
                player.PlayerIndex,
                card.InstanceId,
                state.CurrentPhase)
            : GameEvent.SpellActivated(
                player.PlayerIndex,
                card.InstanceId,
                state.CurrentPhase));
        events.Add(startsChain
            ? GameEvent.ChainStarted(
                player.PlayerIndex,
                card.InstanceId,
                state.CurrentPhase)
            : GameEvent.ChainLinkAdded(
                player.PlayerIndex,
                card.InstanceId,
                state.ActiveChain.Links.Count,
                state.CurrentPhase));
        AdvancePastUnavailableResponders(state, events);
        return ActionResult.Success(events);
    }

    private static ActionResult PassResponse(
        GameState state,
        GameAction action)
    {
        ChainState chain = state.ActiveChain;
        List<GameEvent> events = new List<GameEvent>
        {
            GameEvent.ResponsePassed(action.PlayerIndex, state.CurrentPhase)
        };
        chain.Pass();
        AdvancePastUnavailableResponders(state, events);
        return ActionResult.Success(events);
    }

    private static ActionResult Attack(
        GameState state,
        GameAction action)
    {
        PlayerState attackerOwner = state.Players[action.PlayerIndex];
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
        state.OpenChain(new ChainState(
            ResponseWindowType.AttackDeclaration,
            1 - action.PlayerIndex,
            new PendingAttack(
                action.PlayerIndex,
                action.SourceInstanceId,
                action.TargetInstanceId)));
        events.Add(GameEvent.ChainStarted(
            action.PlayerIndex,
            action.SourceInstanceId,
            state.CurrentPhase));
        AdvancePastUnavailableResponders(state, events);
        return ActionResult.Success(events);
    }

    private static void AdvancePastUnavailableResponders(
        GameState state,
        List<GameEvent> events)
    {
        while (state.ActiveChain != null)
        {
            ChainState chain = state.ActiveChain;
            if (chain.ConsecutivePasses >= 2)
            {
                ResolveChain(state, chain, events);
                return;
            }

            int playerIndex = chain.PriorityPlayerIndex;
            if (GetLegalResponseActions(state, playerIndex).Count > 0)
                return;

            events.Add(GameEvent.ResponsePassed(
                playerIndex,
                state.CurrentPhase));
            chain.Pass();
        }
    }

    private static void ResolvePendingAttack(
        GameState state,
        PendingAttack pendingAttack,
        List<GameEvent> events)
    {
        if (pendingAttack == null)
            return;

        PlayerState attackerOwner = state.Players[pendingAttack.PlayerIndex];
        PlayerState defenderOwner = state.Players[1 - pendingAttack.PlayerIndex];
        int attackerZoneIndex = attackerOwner.FindMonsterZoneIndex(
            pendingAttack.AttackerInstanceId);
        if (attackerZoneIndex < 0)
            return;

        CardInstance attacker = attackerOwner.MonsterZones[attackerZoneIndex];

        if (pendingAttack.TargetInstanceId == 0)
        {
            defenderOwner.TakeDamage(attacker.CurrentAttack);
            events.Add(GameEvent.PlayerDamaged(
                defenderOwner.PlayerIndex,
                attacker.CurrentAttack));
            AddMatchEndIfNeeded(state, attackerOwner, defenderOwner, events);
            return;
        }

        int defenderZoneIndex =
            defenderOwner.FindMonsterZoneIndex(pendingAttack.TargetInstanceId);
        if (defenderZoneIndex < 0)
            return;
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
            SendMonsterToGraveyard(
                state,
                defenderOwner,
                defenderZoneIndex,
                events);
            events.Add(GameEvent.MonsterDestroyed(
                defenderOwner.PlayerIndex,
                defender.InstanceId));

            ResolveTriggeredEffects(
                state,
                attacker,
                EffectTriggerType.OnBattleDestroyedMonster,
                events);
            ResolveTriggeredEffects(
                state,
                defender,
                EffectTriggerType.OnDestroyed,
                events);

            if (destructionPenalty > 0)
            {
                defenderOwner.TakeDamage(destructionPenalty);
                events.Add(GameEvent.PlayerDamaged(
                    defenderOwner.PlayerIndex,
                    destructionPenalty));
            }
        }

        AddMatchEndIfNeeded(state, attackerOwner, defenderOwner, events);
    }

    private static void ResolveChain(
        GameState state,
        ChainState chain,
        List<GameEvent> events)
    {
        for (int index = chain.Links.Count - 1; index >= 0; index--)
        {
            chain.BeginResolvingLink(index);
            ChainLink link = chain.Links[index];
            CardInstance source = state.FindCard(link.SourceInstanceId);
            if (source == null)
                continue;

            if (!link.IsNegated)
            {
                EffectContext context = new EffectContext(
                    state,
                    link.PlayerIndex,
                    link.SourceInstanceId,
                    link.TargetInstanceIds);
                new DefinitionCardEffect(link.Effect).Resolve(context, events);
                ApplyResolvedContinuousEffect(
                    state,
                    source,
                    link,
                    events);
                RefreshEquipModifiersForEffectContext(
                    state,
                    context,
                    events);
            }

            CompleteActivatedCardLifecycle(
                state,
                source,
                link,
                events);
        }

        PendingAttack pendingAttack = chain.PendingAttack;
        state.CloseChain();
        ResolvePendingAttack(state, pendingAttack, events);
    }

    private static void ApplyResolvedContinuousEffect(
        GameState state,
        CardInstance source,
        ChainLink link,
        List<GameEvent> events)
    {
        if (link.Effect.ContinuousModifiers.Count == 0)
            return;

        PlayerState owner = state.Players[link.PlayerIndex];
        if (source.Definition.SpellType == SpellType.Equip)
        {
            if (link.TargetInstanceIds.Count == 0)
                return;
            CardInstance target = state.FindCard(link.TargetInstanceIds[0]);
            if (target == null || target.Zone != CardZone.MonsterZone)
                return;

            source.EquipTo(target.InstanceId);
            ApplyModifierSet(state, source, target, link.Effect, events);
            events.Add(GameEvent.EquipAttached(
                owner.PlayerIndex,
                source.InstanceId,
                target.InstanceId));
            return;
        }

        if (source.Definition.SpellType == SpellType.Field)
        {
            for (int zoneIndex = 0;
                 zoneIndex < owner.MonsterZones.Count;
                 zoneIndex++)
            {
                CardInstance target = owner.MonsterZones[zoneIndex];
                if (target != null)
                    ApplyModifierSet(state, source, target, link.Effect, events);
            }
        }
    }

    private static void ApplyModifierSet(
        GameState state,
        CardInstance source,
        CardInstance target,
        EffectDefinition effect,
        List<GameEvent> events)
    {
        if (state.HasModifierFromSourceToTarget(
                source.InstanceId,
                target.InstanceId))
            return;

        int attackAmount = 0;
        int defenseAmount = 0;
        for (int index = 0;
             index < effect.ContinuousModifiers.Count;
             index++)
        {
            IContinuousModifier modifier = effect.ContinuousModifiers[index];
            attackAmount += modifier.ModifyAttack(source, target);
            defenseAmount += modifier.ModifyDefense(source, target);
        }

        state.ApplyStatModifier(
            source.InstanceId,
            target,
            attackAmount,
            defenseAmount);
        if (attackAmount != 0)
        {
            events.Add(GameEvent.MonsterAttackModified(
                target.OwnerPlayerIndex,
                target.InstanceId,
                attackAmount,
                state.CurrentPhase));
        }
        if (defenseAmount != 0)
        {
            events.Add(GameEvent.MonsterDefenseModified(
                target.OwnerPlayerIndex,
                target.InstanceId,
                defenseAmount,
                state.CurrentPhase));
        }
    }

    private static void CompleteActivatedCardLifecycle(
        GameState state,
        CardInstance source,
        ChainLink link,
        List<GameEvent> events)
    {
        bool remainsOnField = !link.IsNegated &&
            ((source.Definition.Category == CardCategory.Monster &&
              source.Zone == CardZone.MonsterZone) ||
             (source.Definition.Category == CardCategory.Spell &&
              ((source.Definition.SpellType == SpellType.Equip &&
                source.EquippedTargetInstanceId != 0) ||
               (source.Definition.SpellType == SpellType.Field &&
                source.Zone == CardZone.FieldZone))));
        if (remainsOnField)
            return;

        SendSpellTrapToGraveyard(state, source, events);
    }

    private static void SendSpellTrapToGraveyard(
        GameState state,
        CardInstance card,
        List<GameEvent> events)
    {
        PlayerState owner = state.Players[card.OwnerPlayerIndex];
        state.RemovePersistentModifiersFromSource(card.InstanceId);
        if (card.Zone == CardZone.FieldZone)
        {
            owner.MoveFieldSpellToGraveyard(true);
        }
        else if (card.Zone == CardZone.SpellTrapZone)
        {
            int zoneIndex = owner.FindSpellTrapZoneIndex(card.InstanceId);
            if (zoneIndex >= 0)
                owner.MoveSpellTrapToGraveyard(zoneIndex, true);
        }
        else
        {
            return;
        }

        events.Add(GameEvent.CardSentToGraveyard(
            owner.PlayerIndex,
            card.InstanceId,
            state.CurrentPhase));
    }

    private static CardInstance SendMonsterToGraveyard(
        GameState state,
        PlayerState owner,
        int monsterZoneIndex,
        List<GameEvent> events)
    {
        CardInstance monster = owner.MonsterZones[monsterZoneIndex];
        state.RemoveModifiersFromTarget(monster.InstanceId);

        for (int playerIndex = 0;
             playerIndex < state.Players.Count;
             playerIndex++)
        {
            PlayerState player = state.Players[playerIndex];
            for (int zoneIndex = player.SpellTrapZones.Count - 1;
                 zoneIndex >= 0;
                 zoneIndex--)
            {
                CardInstance equip = player.SpellTrapZones[zoneIndex];
                if (equip == null ||
                    equip.EquippedTargetInstanceId != monster.InstanceId)
                    continue;

                state.RemovePersistentModifiersFromSource(equip.InstanceId);
                player.MoveSpellTrapToGraveyard(zoneIndex, true);
                events.Add(GameEvent.CardSentToGraveyard(
                    player.PlayerIndex,
                    equip.InstanceId,
                    state.CurrentPhase));
            }
        }

        return owner.MoveMonsterToGraveyard(monsterZoneIndex);
    }

    private static void ApplyFieldModifiersToMonster(
        GameState state,
        PlayerState owner,
        CardInstance monster,
        List<GameEvent> events)
    {
        CardInstance field = owner.FieldSpell;
        if (field == null)
            return;
        EffectDefinition effect = FindManualActivationEffect(field);
        if (effect != null)
            ApplyModifierSet(state, field, monster, effect, events);
    }

    private static void ResolveFieldMaintenance(
        GameState state,
        int playerIndex,
        List<GameEvent> events)
    {
        PlayerState player = state.Players[playerIndex];
        CardInstance field = player.FieldSpell;
        if (field == null)
            return;

        EffectDefinition maintenance = null;
        for (int index = 0; index < field.Definition.Effects.Count; index++)
        {
            EffectDefinition candidate = field.Definition.Effects[index];
            if (candidate.Trigger == EffectTriggerType.EndPhase)
            {
                maintenance = candidate;
                break;
            }
        }
        if (maintenance == null)
            return;

        EffectContext context = new EffectContext(
            state,
            playerIndex,
            field.InstanceId);
        DefinitionEffectCost cost = new DefinitionEffectCost(maintenance);
        if (cost.CanPay(context, out _))
        {
            int before = player.AvailableEnergy;
            cost.Pay(context, events);
            events.Add(GameEvent.FieldMaintenancePaid(
                playerIndex,
                field.InstanceId,
                before - player.AvailableEnergy));
            return;
        }

        SendSpellTrapToGraveyard(state, field, events);
    }

    private static EffectDefinition FindActivationEffect(
        CardInstance card,
        ChainState chain)
    {
        if (card == null)
            return null;

        EffectActivationWindow window;
        if (chain == null)
        {
            window = EffectActivationWindow.OpenGameState;
        }
        else if (chain.Links.Count > 0)
        {
            window = EffectActivationWindow.ResponseToActivation;
        }
        else
        {
            window = chain.WindowType == ResponseWindowType.AttackDeclaration
                ? EffectActivationWindow.ResponseToAttackDeclaration
                : EffectActivationWindow.ResponseToActivation;
        }
        for (int index = 0; index < card.Definition.Effects.Count; index++)
        {
            EffectDefinition effect = card.Definition.Effects[index];
            bool triggerMatches = chain == null
                ? effect.Trigger == EffectTriggerType.ManualActivation
                : effect.Trigger == EffectTriggerType.OnAttackDeclared ||
                  effect.Trigger == EffectTriggerType.OnSpellTrapActivated;
            if (effect.ActivationWindow == window && triggerMatches)
                return effect;
        }
        return null;
    }

    private static EffectDefinition FindManualActivationEffect(
        CardInstance card)
    {
        if (card == null)
            return null;
        for (int index = 0; index < card.Definition.Effects.Count; index++)
        {
            EffectDefinition effect = card.Definition.Effects[index];
            if (effect.Trigger == EffectTriggerType.ManualActivation &&
                effect.ActivationWindow == EffectActivationWindow.OpenGameState)
                return effect;
        }
        return null;
    }

    private static IReadOnlyList<long> ActionTargets(GameAction action)
    {
        return action.TargetInstanceIds;
    }

    private static string ValidateTriggeredEffectSelection(
        GameState state,
        GameAction action)
    {
        PendingTriggeredEffect pending = state.PendingTriggeredEffect;
        if (pending == null)
            return "There is no triggered effect waiting for a selection.";
        if (action.PlayerIndex != pending.PlayerIndex ||
            action.SourceInstanceId != pending.SourceInstanceId)
        {
            return "This selection does not belong to the pending effect.";
        }
        return pending.ContainsCandidate(action.TargetInstanceId)
            ? string.Empty
            : "The selected card is not eligible for this effect.";
    }

    private static string ValidateTriggeredEffectDecline(
        GameState state,
        GameAction action)
    {
        PendingTriggeredEffect pending = state.PendingTriggeredEffect;
        if (pending == null)
            return "There is no triggered effect to decline.";
        return action.PlayerIndex == pending.PlayerIndex &&
               action.SourceInstanceId == pending.SourceInstanceId
            ? string.Empty
            : "This decline does not belong to the pending effect.";
    }

    private static ActionResult ResolveTriggeredEffect(
        GameState state,
        GameAction action)
    {
        PendingTriggeredEffect pending = state.PendingTriggeredEffect;
        PlayerState player = state.Players[pending.PlayerIndex];
        CardInstance source = state.FindCard(pending.SourceInstanceId);
        List<GameEvent> events = new List<GameEvent>();

        if (ContainsOperation(
                pending.Effect,
                EffectOperationType.SearchBasicMonsterFromDeck))
        {
            CardInstance selected = player.MoveDeckCardToHand(
                action.TargetInstanceId);
            if (selected != null)
            {
                events.Add(GameEvent.CardAddedToHand(
                    player.PlayerIndex,
                    selected.InstanceId,
                    state.CurrentPhase));
                state.Random.Shuffle(player.MutableDeck);
                events.Add(GameEvent.DeckShuffled(
                    player.PlayerIndex,
                    state.CurrentPhase));
            }
        }
        else if (ContainsOperation(
                     pending.Effect,
                     EffectOperationType.RecoverBasicMonsterFromGraveyard))
        {
            CardInstance selected = player.MoveGraveyardCardToHand(
                action.TargetInstanceId);
            if (selected != null)
            {
                events.Add(GameEvent.CardAddedToHand(
                    player.PlayerIndex,
                    selected.InstanceId,
                    state.CurrentPhase));
            }
        }

        if (source != null)
            state.RecordEffectUse(pending.Effect, player.PlayerIndex, source);
        events.Add(GameEvent.EffectResolved(
            player.PlayerIndex,
            pending.SourceInstanceId,
            state.CurrentPhase));
        state.CloseTriggeredEffect();
        return ActionResult.Success(events);
    }

    private static ActionResult DeclineTriggeredEffect(
        GameState state,
        GameAction action)
    {
        List<GameEvent> events = new List<GameEvent>
        {
            GameEvent.TriggeredEffectDeclined(
                action.PlayerIndex,
                action.SourceInstanceId,
                state.CurrentPhase)
        };
        state.CloseTriggeredEffect();
        return ActionResult.Success(events);
    }

    private static void ResolveTriggeredEffects(
        GameState state,
        CardInstance source,
        EffectTriggerType trigger,
        List<GameEvent> events,
        int triggerValue = 0)
    {
        if (source == null)
            return;

        for (int index = 0; index < source.Definition.Effects.Count; index++)
        {
            EffectDefinition effect = source.Definition.Effects[index];
            if (effect.Trigger != trigger ||
                !state.CanUseEffect(effect, source.OwnerPlayerIndex, source))
            {
                continue;
            }

            List<long> candidates = BuildTriggeredEffectCandidates(
                state,
                source,
                effect);
            bool isOptionalSelection = ContainsOperation(
                    effect,
                    EffectOperationType.SearchBasicMonsterFromDeck) ||
                ContainsOperation(
                    effect,
                    EffectOperationType.RecoverBasicMonsterFromGraveyard);
            if (isOptionalSelection)
            {
                if (candidates.Count == 0 || state.HasPendingTriggeredEffect)
                    continue;
                state.OpenTriggeredEffect(new PendingTriggeredEffect(
                    source.OwnerPlayerIndex,
                    source.InstanceId,
                    effect,
                    candidates));
                events.Add(GameEvent.TriggeredEffectOffered(
                    source.OwnerPlayerIndex,
                    source.InstanceId,
                    state.CurrentPhase));
                continue;
            }

            EffectContext context = new EffectContext(
                state,
                source.OwnerPlayerIndex,
                source.InstanceId,
                triggerValue: triggerValue);
            new DefinitionCardEffect(effect).Resolve(context, events);
            state.RecordEffectUse(
                effect,
                source.OwnerPlayerIndex,
                source);
            RefreshEquipModifiersForEffectContext(state, context, events);
        }
    }

    private static List<long> BuildTriggeredEffectCandidates(
        GameState state,
        CardInstance source,
        EffectDefinition effect)
    {
        List<long> candidates = new List<long>();
        HashSet<string> includedCardIds = new HashSet<string>();
        PlayerState owner = state.Players[source.OwnerPlayerIndex];
        IReadOnlyList<CardInstance> pool;

        if (ContainsOperation(
                effect,
                EffectOperationType.SearchBasicMonsterFromDeck))
        {
            pool = owner.Deck;
        }
        else if (ContainsOperation(
                     effect,
                     EffectOperationType.RecoverBasicMonsterFromGraveyard))
        {
            pool = owner.Graveyard;
        }
        else
        {
            return candidates;
        }

        for (int index = 0; index < pool.Count; index++)
        {
            CardInstance card = pool[index];
            if (!card.Definition.IsMonster ||
                card.Definition.MonsterRank != MonsterRank.Basic)
            {
                continue;
            }
            if (effect.Operations.Count > 0 &&
                effect.Operations[0].Type ==
                    EffectOperationType.SearchBasicMonsterFromDeck &&
                card.Definition.CardId == source.Definition.CardId)
            {
                continue;
            }
            if (includedCardIds.Add(card.Definition.CardId))
                candidates.Add(card.InstanceId);
        }
        return candidates;
    }

    private static bool IsLegalAttackResponseTarget(
        GameState state,
        int activatingPlayerIndex,
        EffectDefinition effect,
        IReadOnlyList<long> targets)
    {
        PendingAttack attack = state.ActiveChain.PendingAttack;
        if (attack == null || targets.Count != 1)
            return false;
        if (effect.TargetType == EffectTargetType.OwnMonster)
        {
            return attack.TargetInstanceId != 0 &&
                targets[0] == attack.TargetInstanceId &&
                state.Players[activatingPlayerIndex]
                    .FindMonsterZoneIndex(targets[0]) >= 0;
        }
        return effect.TargetType == EffectTargetType.OpponentMonster &&
            targets[0] == attack.AttackerInstanceId;
    }

    private static void RefreshEquipModifiersForEffectContext(
        GameState state,
        EffectContext context,
        List<GameEvent> events)
    {
        CardInstance source = state.FindCard(context.SourceInstanceId);
        if (source != null && source.Zone == CardZone.MonsterZone)
            RefreshEquipModifiersForTarget(state, source, events);
        for (int index = 0; index < context.TargetInstanceIds.Count; index++)
        {
            CardInstance target = state.FindCard(context.TargetInstanceIds[index]);
            if (target != null && target.Zone == CardZone.MonsterZone)
                RefreshEquipModifiersForTarget(state, target, events);
        }
    }

    private static void RefreshEquipModifiersForTarget(
        GameState state,
        CardInstance target,
        List<GameEvent> events)
    {
        for (int playerIndex = 0; playerIndex < state.Players.Count; playerIndex++)
        {
            PlayerState player = state.Players[playerIndex];
            for (int zoneIndex = 0;
                 zoneIndex < player.SpellTrapZones.Count;
                 zoneIndex++)
            {
                CardInstance equip = player.SpellTrapZones[zoneIndex];
                if (equip == null ||
                    equip.EquippedTargetInstanceId != target.InstanceId)
                {
                    continue;
                }

                EffectDefinition effect = FindManualActivationEffect(equip);
                if (effect == null || effect.ContinuousModifiers.Count == 0)
                    continue;
                state.RemovePersistentModifiersFromSource(equip.InstanceId);
                ApplyModifierSet(state, equip, target, effect, events);
            }
        }
    }

    private static bool IsAllowedTurn(
        EffectTurnScope scope,
        GameState state,
        int playerIndex)
    {
        return scope == EffectTurnScope.EitherTurn ||
            (scope == EffectTurnScope.YourTurn &&
             playerIndex == state.CurrentPlayerIndex) ||
            (scope == EffectTurnScope.OpponentTurn &&
             playerIndex != state.CurrentPlayerIndex);
    }

    private static EffectPhaseMask PhaseMask(TurnPhase phase)
    {
        switch (phase)
        {
            case TurnPhase.Recovery:
                return EffectPhaseMask.Recovery;
            case TurnPhase.Standby:
                return EffectPhaseMask.Standby;
            case TurnPhase.Main:
                return EffectPhaseMask.Main;
            case TurnPhase.Battle:
                return EffectPhaseMask.Battle;
            case TurnPhase.End:
                return EffectPhaseMask.End;
            default:
                return EffectPhaseMask.None;
        }
    }

    private static bool ContainsOperation(
        EffectDefinition effect,
        EffectOperationType operationType)
    {
        for (int index = 0; index < effect.Operations.Count; index++)
        {
            if (effect.Operations[index].Type == operationType)
                return true;
        }
        return false;
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
