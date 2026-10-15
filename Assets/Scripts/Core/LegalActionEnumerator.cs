using System.Collections.Generic;

// Builds complete, currently legal player decisions without mutating GameState.
// Presentation code may group these actions into multi-step windows, while
// bots, simulations, replay and networking can consume the same source.
internal static class LegalActionEnumerator
{
    private static readonly int[] PreferredZoneOrder = { 1, 2, 0 };

    public static IReadOnlyList<GameAction> GetLegalActions(
        GameState state,
        int playerIndex)
    {
        List<GameAction> actions = new List<GameAction>();
        if (state == null || state.IsFinished ||
            playerIndex < 0 || playerIndex >= state.Players.Count ||
            state.InteractionPlayerIndex != playerIndex)
        {
            return actions;
        }

        if (state.HasOpenResponseWindow)
        {
            IReadOnlyList<GameAction> responses =
                GameRules.GetLegalResponseActions(state, playerIndex);
            if (responses.Count == 0)
                return actions;

            for (int index = 0; index < responses.Count; index++)
                actions.Add(responses[index]);

            AddIfLegal(
                state,
                actions,
                GameAction.PassResponse(playerIndex));
            return actions;
        }

        if (state.HasPendingTriggeredEffect)
        {
            PendingTriggeredEffect pending = state.PendingTriggeredEffect;
            for (int index = 0;
                 index < pending.CandidateInstanceIds.Count;
                 index++)
            {
                AddIfLegal(
                    state,
                    actions,
                    GameAction.ResolveTriggeredEffect(
                        playerIndex,
                        pending.SourceInstanceId,
                        pending.CandidateInstanceIds[index]));
            }
            AddIfLegal(
                state,
                actions,
                GameAction.DeclineTriggeredEffect(
                    playerIndex,
                    pending.SourceInstanceId));
            return actions;
        }

        if (playerIndex != state.CurrentPlayerIndex)
            return actions;

        if (state.CurrentPhase == TurnPhase.Main)
        {
            AddMainPhaseActions(state, playerIndex, actions);
            AddMainPhaseTransitions(state, playerIndex, actions);
        }
        else if (state.CurrentPhase == TurnPhase.Battle)
        {
            AddAttackActions(state, playerIndex, actions);
            AddIfLegal(
                state,
                actions,
                GameAction.EnterEndPhase(playerIndex));
        }
        else if (state.CurrentPhase == TurnPhase.End)
        {
            AddIfLegal(state, actions, GameAction.EndTurn(playerIndex));
        }

        return actions;
    }

    private static void AddMainPhaseActions(
        GameState state,
        int playerIndex,
        List<GameAction> actions)
    {
        PlayerState player = state.Players[playerIndex];

        for (int index = 0; index < player.Hand.Count; index++)
        {
            CardInstance card = player.Hand[index];
            if (card.Definition.Category == CardCategory.Monster)
                AddSummonActions(state, playerIndex, card, actions);
            else if (card.Definition.Category == CardCategory.Spell)
                AddOpenActivationActions(state, playerIndex, card, actions);
            else if (card.Definition.Category == CardCategory.Trap)
                AddTrapSetAction(state, playerIndex, card, actions);
        }

        for (int zoneIndex = 0;
             zoneIndex < player.MonsterZones.Count;
             zoneIndex++)
        {
            CardInstance monster = player.MonsterZones[zoneIndex];
            if (monster == null)
                continue;

            for (int amount = 1; amount <= player.AvailableEnergy; amount++)
            {
                AddIfLegal(
                    state,
                    actions,
                    GameAction.AllocateEnergy(
                        playerIndex,
                        monster.InstanceId,
                        amount));
            }

            BattlePosition opposite =
                monster.BattlePosition == BattlePosition.Attack
                    ? BattlePosition.Defense
                    : BattlePosition.Attack;
            AddIfLegal(
                state,
                actions,
                GameAction.ChangeBattlePosition(
                    playerIndex,
                    monster.InstanceId,
                    opposite));

            AddOpenActivationActions(
                state,
                playerIndex,
                monster,
                actions);
        }
    }

    private static void AddMainPhaseTransitions(
        GameState state,
        int playerIndex,
        List<GameAction> actions)
    {
        PlayerState player = state.Players[playerIndex];
        bool battleIsAvailable = state.TurnNumber != 1 &&
            !player.IsBattlePhaseSkipScheduled(state.TurnNumber);
        if (battleIsAvailable)
        {
            AddIfLegal(
                state,
                actions,
                GameAction.AdvancePhase(playerIndex));
        }

        AddIfLegal(
            state,
            actions,
            GameAction.EnterEndPhase(playerIndex));
    }

    private static void AddSummonActions(
        GameState state,
        int playerIndex,
        CardInstance monster,
        List<GameAction> actions)
    {
        PlayerState player = state.Players[playerIndex];
        List<long[]> tributeChoices = BuildTributeChoices(
            player,
            monster.Definition.TributeCount);
        BattlePosition[] positions =
        {
            BattlePosition.Attack,
            BattlePosition.Defense
        };

        for (int tributeIndex = 0;
             tributeIndex < tributeChoices.Count;
             tributeIndex++)
        {
            long[] tributes = tributeChoices[tributeIndex];
            for (int positionIndex = 0;
                 positionIndex < positions.Length;
                 positionIndex++)
            {
                for (int zoneOrderIndex = 0;
                     zoneOrderIndex < PreferredZoneOrder.Length;
                     zoneOrderIndex++)
                {
                    int zoneIndex = PreferredZoneOrder[zoneOrderIndex];
                    if (zoneIndex < 0 ||
                        zoneIndex >= player.MonsterZones.Count)
                    {
                        continue;
                    }

                    GameAction action = GameAction.SummonMonster(
                        playerIndex,
                        monster.InstanceId,
                        zoneIndex,
                        positions[positionIndex],
                        tributes);
                    if (AddIfLegal(state, actions, action))
                        break;
                }
            }
        }
    }

    private static void AddOpenActivationActions(
        GameState state,
        int playerIndex,
        CardInstance card,
        List<GameAction> actions)
    {
        EffectDefinition effect = FindOpenActivationEffect(card);
        if (effect == null)
            return;

        int zoneIndex = -1;
        if (card.Zone == CardZone.Hand &&
            card.Definition.Category == CardCategory.Spell &&
            card.Definition.SpellType != SpellType.Field)
        {
            zoneIndex = FindPreferredSpellTrapZone(
                state.Players[playerIndex]);
            if (zoneIndex < 0)
                return;
        }

        if (effect.TargetCount == 0)
        {
            AddIfLegal(
                state,
                actions,
                GameAction.ActivateCard(
                    playerIndex,
                    card.InstanceId,
                    zoneIndex));
            return;
        }

        int targetOwnerIndex;
        if (effect.TargetType == EffectTargetType.OwnMonster)
            targetOwnerIndex = playerIndex;
        else if (effect.TargetType == EffectTargetType.OpponentMonster)
            targetOwnerIndex = 1 - playerIndex;
        else
            return;

        PlayerState targetOwner = state.Players[targetOwnerIndex];
        List<long> candidates = new List<long>();
        for (int targetZone = 0;
             targetZone < targetOwner.MonsterZones.Count;
             targetZone++)
        {
            CardInstance target = targetOwner.MonsterZones[targetZone];
            if (target != null)
                candidates.Add(target.InstanceId);
        }

        AddTargetPermutations(
            state,
            actions,
            playerIndex,
            card.InstanceId,
            zoneIndex,
            candidates,
            effect.TargetCount,
            new List<long>());
    }

    private static void AddTrapSetAction(
        GameState state,
        int playerIndex,
        CardInstance trap,
        List<GameAction> actions)
    {
        int zoneIndex = FindPreferredSpellTrapZone(
            state.Players[playerIndex]);
        if (zoneIndex < 0)
            return;

        AddIfLegal(
            state,
            actions,
            GameAction.SetTrap(playerIndex, trap.InstanceId, zoneIndex));
    }

    private static void AddAttackActions(
        GameState state,
        int playerIndex,
        List<GameAction> actions)
    {
        PlayerState player = state.Players[playerIndex];
        PlayerState opponent = state.Players[1 - playerIndex];
        bool opponentHasMonster = opponent.HasAnyMonster();

        for (int attackerZone = 0;
             attackerZone < player.MonsterZones.Count;
             attackerZone++)
        {
            CardInstance attacker = player.MonsterZones[attackerZone];
            if (attacker == null)
                continue;

            if (!opponentHasMonster)
            {
                AddIfLegal(
                    state,
                    actions,
                    GameAction.Attack(playerIndex, attacker.InstanceId));
                continue;
            }

            for (int targetZone = 0;
                 targetZone < opponent.MonsterZones.Count;
                 targetZone++)
            {
                CardInstance target = opponent.MonsterZones[targetZone];
                if (target == null)
                    continue;

                AddIfLegal(
                    state,
                    actions,
                    GameAction.Attack(
                        playerIndex,
                        attacker.InstanceId,
                        target.InstanceId));
            }
        }
    }

    private static EffectDefinition FindOpenActivationEffect(
        CardInstance card)
    {
        if (card == null)
            return null;

        for (int index = 0; index < card.Definition.Effects.Count; index++)
        {
            EffectDefinition effect = card.Definition.Effects[index];
            if (effect.ActivationWindow ==
                    EffectActivationWindow.OpenGameState &&
                effect.Trigger == EffectTriggerType.ManualActivation)
            {
                return effect;
            }
        }

        return null;
    }

    private static void AddTargetPermutations(
        GameState state,
        List<GameAction> actions,
        int playerIndex,
        long sourceInstanceId,
        int zoneIndex,
        IReadOnlyList<long> candidates,
        int requiredCount,
        List<long> selected)
    {
        if (selected.Count == requiredCount)
        {
            AddIfLegal(
                state,
                actions,
                GameAction.ActivateCardWithTargets(
                    playerIndex,
                    sourceInstanceId,
                    zoneIndex,
                    selected.ToArray()));
            return;
        }

        for (int index = 0; index < candidates.Count; index++)
        {
            long candidate = candidates[index];
            if (selected.Contains(candidate))
                continue;
            selected.Add(candidate);
            AddTargetPermutations(
                state,
                actions,
                playerIndex,
                sourceInstanceId,
                zoneIndex,
                candidates,
                requiredCount,
                selected);
            selected.RemoveAt(selected.Count - 1);
        }
    }

    private static int FindPreferredSpellTrapZone(PlayerState player)
    {
        for (int index = 0; index < PreferredZoneOrder.Length; index++)
        {
            int zoneIndex = PreferredZoneOrder[index];
            if (zoneIndex >= 0 &&
                zoneIndex < player.SpellTrapZones.Count &&
                player.SpellTrapZones[zoneIndex] == null)
            {
                return zoneIndex;
            }
        }

        return -1;
    }

    private static List<long[]> BuildTributeChoices(
        PlayerState player,
        int requiredCount)
    {
        List<long> available = new List<long>();
        for (int zoneIndex = 0;
             zoneIndex < player.MonsterZones.Count;
             zoneIndex++)
        {
            CardInstance monster = player.MonsterZones[zoneIndex];
            if (monster != null)
                available.Add(monster.InstanceId);
        }

        List<long[]> results = new List<long[]>();
        AddTributeChoices(
            available,
            requiredCount,
            0,
            new List<long>(),
            results);
        return results;
    }

    private static void AddTributeChoices(
        IReadOnlyList<long> available,
        int requiredCount,
        int startIndex,
        List<long> current,
        List<long[]> results)
    {
        if (current.Count == requiredCount)
        {
            results.Add(current.ToArray());
            return;
        }

        for (int index = startIndex; index < available.Count; index++)
        {
            current.Add(available[index]);
            AddTributeChoices(
                available,
                requiredCount,
                index + 1,
                current,
                results);
            current.RemoveAt(current.Count - 1);
        }
    }

    private static bool AddIfLegal(
        GameState state,
        List<GameAction> actions,
        GameAction action)
    {
        if (!string.IsNullOrEmpty(GameRules.ValidateAction(state, action)))
            return false;

        actions.Add(action);
        return true;
    }
}
