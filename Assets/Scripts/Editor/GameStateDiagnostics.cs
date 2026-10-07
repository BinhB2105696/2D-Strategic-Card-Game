using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class GameStateDiagnostics
{
    private const uint TestSeed = 20260930u;

    [MenuItem("Card Game/Diagnostics/Run Game Initialization Smoke Test")]
    public static void RunInitializationSmokeTest()
    {
        List<CardDefinition> availableCards = LoadDefinitions();

        if (availableCards.Count == 0)
        {
            Debug.LogError(
                "Game initialization test needs at least one CardData asset.");
            return;
        }

        GameConfig config = GameConfig.CreateDefault();
        List<CardDefinition> firstDeck =
            BuildTestDeck(availableCards, config.MinimumDeckSize, 0);
        List<CardDefinition> secondDeck =
            BuildTestDeck(availableCards, config.MinimumDeckSize, 1);

        GameState firstRun = GameState.CreateNewMatch(
            config,
            TestSeed,
            firstDeck,
            secondDeck);
        GameState secondRun = GameState.CreateNewMatch(
            config,
            TestSeed,
            firstDeck,
            secondDeck);

        string firstSignature = CreateOpeningSignature(firstRun);
        string secondSignature = CreateOpeningSignature(secondRun);
        bool deterministic = firstSignature == secondSignature;

        if (!deterministic)
        {
            Debug.LogError(
                "Game initialization is not deterministic for the same seed.");
            return;
        }

        Debug.Log(
            "Game initialization smoke test passed.\n" +
            $"Seed: {firstRun.Random.InitialSeed}, RNG version: " +
            $"{SeededGameRandom.AlgorithmVersion}\n" +
            $"First player: Player {firstRun.CurrentPlayerIndex}\n" +
            DescribePlayer(firstRun.Players[0]) + "\n" +
            DescribePlayer(firstRun.Players[1]) + "\n" +
            "Same seed produced the same starting hands: True");
    }

    [MenuItem("Card Game/Diagnostics/Run Turn Flow Smoke Test")]
    public static void RunTurnFlowSmokeTest()
    {
        List<CardDefinition> availableCards = LoadDefinitions();

        if (availableCards.Count == 0)
        {
            Debug.LogError(
                "Turn flow test needs at least one CardData asset.");
            return;
        }

        GameConfig config = GameConfig.CreateDefault();
        List<CardDefinition> firstDeck =
            BuildTestDeck(availableCards, config.MinimumDeckSize, 0);
        List<CardDefinition> secondDeck =
            BuildTestDeck(availableCards, config.MinimumDeckSize, 1);
        GameSession session = GameSession.Create(
            config,
            TestSeed,
            firstDeck,
            secondDeck);

        int firstPlayer = session.State.CurrentPlayerIndex;
        int secondPlayer = 1 - firstPlayer;

        ActionResult rejectedAction =
            session.TryApply(GameAction.AdvancePhase(secondPlayer));

        if (rejectedAction.Succeeded || session.Log.Actions.Count != 0)
        {
            Debug.LogError(
                "Turn flow test failed to reject an out-of-turn action.");
            return;
        }

        StartTurnToMain(session, firstPlayer);
        FinishTurn(session, firstPlayer);
        StartTurnToMain(session, secondPlayer);

        bool stateIsCorrect =
            session.State.TurnNumber == 2 &&
            session.State.CurrentPlayerIndex == secondPlayer &&
            session.State.CurrentPhase == TurnPhase.Main &&
            session.State.Players[firstPlayer].Hand.Count == 5 &&
            session.State.Players[secondPlayer].Hand.Count == 5 &&
            session.Log.Actions.Count == 8;

        if (!stateIsCorrect)
        {
            Debug.LogError("Turn flow smoke test produced an invalid state.");
            return;
        }

        Debug.Log(
            "Turn flow smoke test passed.\n" +
            $"Turn: {session.State.TurnNumber}\n" +
            $"Current player: Player {session.State.CurrentPlayerIndex}\n" +
            $"Current phase: {session.State.CurrentPhase}\n" +
            $"Player {firstPlayer} hand: " +
            $"{session.State.Players[firstPlayer].Hand.Count}\n" +
            $"Player {secondPlayer} hand: " +
            $"{session.State.Players[secondPlayer].Hand.Count}\n" +
            $"Logged actions: {session.Log.Actions.Count}\n" +
            "Out-of-turn action rejected and not logged: True\n" +
            "Turn 1 flow: Recovery -> Standby -> Main -> End -> next turn");
    }

    [MenuItem("Card Game/Diagnostics/Run Recovery Skip Smoke Test")]
    public static void RunRecoverySkipSmokeTest()
    {
        List<CardDefinition> availableCards = LoadDefinitions();

        if (availableCards.Count == 0)
        {
            Debug.LogError("Recovery skip test needs CardData assets.");
            return;
        }

        GameConfig config = GameConfig.CreateDefault();
        List<CardDefinition> deck =
            BuildTestDeck(availableCards, config.MinimumDeckSize, 0);
        GameSession session = GameSession.Create(
            config,
            TestSeed,
            deck,
            deck);
        int firstPlayer = session.State.CurrentPlayerIndex;
        int secondPlayer = 1 - firstPlayer;
        PlayerState firstState = session.State.Players[firstPlayer];
        PlayerState secondState = session.State.Players[secondPlayer];

        firstState.TrySpendEnergy(2);
        firstState.ScheduleDrawStepSkip(session.State.TurnNumber);
        firstState.ScheduleEnergyRefreshStepSkip(session.State.TurnNumber);
        firstState.ScheduleStandbyPhaseSkip(session.State.TurnNumber);
        int firstHandBefore = firstState.Hand.Count;

        ApplyRequired(session, GameAction.ResolveDrawStep(firstPlayer));
        ApplyRequired(
            session,
            GameAction.ResolveEnergyRefreshStep(firstPlayer));

        bool recoverySkipsAreCorrect =
            firstState.Hand.Count == firstHandBefore &&
            firstState.AvailableEnergy == 3 &&
            session.State.CurrentPhase == TurnPhase.Main &&
            !firstState.IsDrawStepSkipScheduled(session.State.TurnNumber) &&
            !firstState.IsEnergyRefreshStepSkipScheduled(session.State.TurnNumber) &&
            !firstState.IsStandbyPhaseSkipScheduled(session.State.TurnNumber);

        if (!recoverySkipsAreCorrect)
        {
            Debug.LogError("Recovery or Standby skip behavior is incorrect.");
            return;
        }

        FinishTurn(session, firstPlayer);
        secondState.ScheduleBattlePhaseSkip(session.State.TurnNumber);
        StartTurnToMain(session, secondPlayer);
        ApplyRequired(session, GameAction.AdvancePhase(secondPlayer));

        if (session.State.CurrentPhase != TurnPhase.End ||
            secondState.IsBattlePhaseSkipScheduled(session.State.TurnNumber))
        {
            Debug.LogError("Battle Phase skip behavior is incorrect.");
            return;
        }

        Debug.Log(
            "Recovery skip smoke test passed.\n" +
            "Draw Step skipped independently: True\n" +
            "Energy Refresh Step skipped independently: True\n" +
            "Standby Phase skipped: True\n" +
            "Battle Phase skipped: True\n" +
            "Main and End Phase remained available: True");
    }

    [MenuItem("Card Game/Diagnostics/Run Direct End Phase Smoke Test")]
    public static void RunDirectEndPhaseSmokeTest()
    {
        List<CardDefinition> availableCards = LoadDefinitions();
        if (availableCards.Count == 0)
        {
            Debug.LogError("Direct End Phase test needs CardData assets.");
            return;
        }

        GameConfig config = GameConfig.CreateDefault();
        List<CardDefinition> deck =
            BuildTestDeck(availableCards, config.MinimumDeckSize, 0);
        GameSession session = GameSession.Create(config, TestSeed, deck, deck);
        int firstPlayer = session.State.CurrentPlayerIndex;
        int secondPlayer = 1 - firstPlayer;

        ActionResult recoveryRejected =
            session.TryApply(GameAction.EnterEndPhase(firstPlayer));
        if (recoveryRejected.Succeeded || session.Log.Actions.Count != 0)
        {
            Debug.LogError("Direct End Phase was accepted during Recovery.");
            return;
        }

        StartTurnToMain(session, firstPlayer);
        PlayerState firstState = session.State.Players[firstPlayer];
        firstState.ScheduleBattlePhaseSkip(session.State.TurnNumber);
        firstState.ScheduleBattlePhaseSkip(session.State.TurnNumber);
        int loggedBefore = session.Log.Actions.Count;
        ActionResult wrongPlayer =
            session.TryApply(GameAction.EnterEndPhase(secondPlayer));
        ActionResult turnOneEnd =
            session.TryApply(GameAction.EnterEndPhase(firstPlayer));
        bool turnOneCorrect =
            !wrongPlayer.Succeeded &&
            turnOneEnd.Succeeded &&
            session.State.CurrentPhase == TurnPhase.End &&
            !firstState.IsBattlePhaseSkipScheduled(session.State.TurnNumber) &&
            turnOneEnd.Events.Count == 2 &&
            turnOneEnd.Events[0].Type == GameEventType.BattlePhaseSkipped &&
            turnOneEnd.Events[1].Type == GameEventType.PhaseChanged &&
            session.Log.Actions.Count == loggedBefore + 1;

        ActionResult repeated =
            session.TryApply(GameAction.EnterEndPhase(firstPlayer));
        if (!turnOneCorrect || repeated.Succeeded ||
            session.Log.Actions.Count != loggedBefore + 1)
        {
            Debug.LogError("Turn 1 direct End Phase rules or logging failed.");
            return;
        }

        ApplyRequired(session, GameAction.EndTurn(firstPlayer));
        StartTurnToMain(session, secondPlayer);
        PlayerState secondState = session.State.Players[secondPlayer];
        secondState.ScheduleBattlePhaseSkip(session.State.TurnNumber);
        secondState.ScheduleBattlePhaseSkip(session.State.TurnNumber);
        ActionResult turnTwoEnd =
            session.TryApply(GameAction.EnterEndPhase(secondPlayer));
        if (!turnTwoEnd.Succeeded ||
            session.State.CurrentPhase != TurnPhase.End ||
            secondState.IsBattlePhaseSkipScheduled(session.State.TurnNumber) ||
            turnTwoEnd.Events.Count != 2 ||
            turnTwoEnd.Events[0].Type != GameEventType.BattlePhaseSkipped)
        {
            Debug.LogError("Turn 2 direct End Phase did not consume Battle skip.");
            return;
        }

        ApplyRequired(session, GameAction.EndTurn(secondPlayer));
        StartTurnToMain(session, firstPlayer);
        ApplyRequired(session, GameAction.AdvancePhase(firstPlayer));
        if (session.State.CurrentPhase != TurnPhase.Battle)
        {
            Debug.LogError("Turn 1 Battle skip leaked into the next own turn.");
            return;
        }
        ApplyRequired(session, GameAction.AdvancePhase(firstPlayer));
        ApplyRequired(session, GameAction.EndTurn(firstPlayer));
        StartTurnToMain(session, secondPlayer);
        ApplyRequired(session, GameAction.AdvancePhase(secondPlayer));
        ActionResult fromBattle =
            session.TryApply(GameAction.EnterEndPhase(secondPlayer));
        if (!fromBattle.Succeeded ||
            session.State.CurrentPhase != TurnPhase.End ||
            fromBattle.Events.Count != 1 ||
            fromBattle.Events[0].Type != GameEventType.PhaseChanged)
        {
            Debug.LogError("Battle to End Phase transition is incorrect.");
            return;
        }

        Debug.Log(
            "Direct End Phase smoke test passed.\n" +
            "Recovery and out-of-turn requests rejected: True\n" +
            "Main to End skips Battle without entering it: True\n" +
            "Turn 1 Battle skip applied and not deferred: True\n" +
            "Repeated Battle skips on one turn do not stack: True\n" +
            "Battle to End has no Battle-skipped event: True\n" +
            "Only valid actions logged: True");
    }

    [MenuItem("Card Game/Diagnostics/Run Main Phase Actions Smoke Test")]
    public static void RunMainPhaseActionsSmokeTest()
    {
        List<CardDefinition> availableCards = LoadDefinitions();
        CardDefinition basicMonster = availableCards.Find(definition =>
            definition.IsMonster && definition.TributeCount == 0);

        if (basicMonster == null)
        {
            Debug.LogError(
                "Main Phase test needs at least one Basic Monster CardData.");
            return;
        }

        GameConfig config = GameConfig.CreateDefault();
        List<CardDefinition> monsterDeck = new List<CardDefinition>();

        for (int index = 0; index < config.MinimumDeckSize; index++)
        {
            monsterDeck.Add(basicMonster);
        }

        GameSession session = GameSession.Create(
            config,
            TestSeed,
            monsterDeck,
            monsterDeck);
        int firstPlayer = session.State.CurrentPlayerIndex;
        int secondPlayer = 1 - firstPlayer;

        StartTurnToMain(session, firstPlayer);
        CardInstance monster =
            session.State.Players[firstPlayer].Hand[0];
        ApplyRequired(session, GameAction.SummonMonster(
            firstPlayer,
            monster.InstanceId,
            0,
            BattlePosition.Attack));
        ApplyRequired(session, GameAction.AllocateEnergy(
            firstPlayer,
            monster.InstanceId,
            2));

        int logCountBeforeRejectedAction = session.Log.Actions.Count;
        ActionResult rejectedPositionChange = session.TryApply(
            GameAction.ChangeBattlePosition(
                firstPlayer,
                monster.InstanceId,
                BattlePosition.Defense));

        if (rejectedPositionChange.Succeeded ||
            session.Log.Actions.Count != logCountBeforeRejectedAction)
        {
            Debug.LogError(
                "A newly summoned Monster incorrectly changed position.");
            return;
        }

        FinishTurn(session, firstPlayer);
        StartTurnToMain(session, secondPlayer);
        FinishTurn(session, secondPlayer);
        StartTurnToMain(session, firstPlayer);
        ApplyRequired(session, GameAction.ChangeBattlePosition(
            firstPlayer,
            monster.InstanceId,
            BattlePosition.Defense));

        PlayerState owner = session.State.Players[firstPlayer];
        bool stateIsCorrect =
            session.State.TurnNumber == 3 &&
            session.State.CurrentPlayerIndex == firstPlayer &&
            session.State.CurrentPhase == TurnPhase.Main &&
            owner.MonsterZones[0] == monster &&
            monster.AllocatedEnergy == 2 &&
            monster.BattlePosition == BattlePosition.Defense &&
            monster.HasChangedPositionThisTurn &&
            !monster.WasSummonedThisTurn &&
            owner.AvailableEnergy == owner.MaxEnergy;

        if (!stateIsCorrect)
        {
            Debug.LogError(
                "Main Phase actions smoke test produced an invalid state.");
            return;
        }

        Debug.Log(
            "Main Phase actions smoke test passed.\n" +
            $"Monster: {monster.Definition.CardName}, Instance: " +
            $"{monster.InstanceId}\n" +
            "Summon cost paid: 1 Energy\n" +
            $"Allocated Energy retained: {monster.AllocatedEnergy}\n" +
            "Position change on summon turn rejected: True\n" +
            $"Position on next owner turn: {monster.BattlePosition}\n" +
            $"Available Energy after next reset: {owner.AvailableEnergy}/" +
            $"{owner.MaxEnergy}\n" +
            $"Logged valid actions: {session.Log.Actions.Count}");
    }

    [MenuItem("Card Game/Diagnostics/Run Tribute Summon Smoke Test")]
    public static void RunTributeSummonSmokeTest()
    {
        List<CardDefinition> definitions = LoadDefinitions();
        CardDefinition basicMonster = definitions.Find(definition =>
            definition.IsMonster && definition.TributeCount == 0);
        CardDefinition advancedMonster = definitions.Find(definition =>
            definition.IsMonster && definition.TributeCount == 1);

        if (basicMonster == null || advancedMonster == null)
        {
            Debug.LogError(
                "Tribute test needs both a Basic and an Advanced Monster.");
            return;
        }

        GameConfig config = CreateTestConfig(startingHandSize: 30);
        List<CardDefinition> deck = new List<CardDefinition>();

        for (int index = 0; index < config.MinimumDeckSize; index++)
        {
            deck.Add(index % 2 == 0 ? basicMonster : advancedMonster);
        }

        GameSession session = GameSession.Create(
            config,
            TestSeed,
            deck,
            deck);
        int playerIndex = session.State.CurrentPlayerIndex;
        PlayerState player = session.State.Players[playerIndex];
        StartTurnToMain(session, playerIndex);

        CardInstance basic = FindHandCard(player, tributeCount: 0);
        CardInstance advanced = FindHandCard(player, tributeCount: 1);
        ApplyRequired(session, GameAction.SummonMonster(
            playerIndex,
            basic.InstanceId,
            0,
            BattlePosition.Attack));
        ApplyRequired(session, GameAction.SummonMonster(
            playerIndex,
            advanced.InstanceId,
            0,
            BattlePosition.Attack,
            basic.InstanceId));

        bool stateIsCorrect =
            player.MonsterZones[0] == advanced &&
            player.Graveyard.Count == 1 &&
            player.Graveyard[0] == basic &&
            player.AvailableEnergy == 3;

        if (!stateIsCorrect)
        {
            Debug.LogError(
                "Tribute Summon smoke test produced an invalid state.");
            return;
        }

        Debug.Log(
            "Tribute Summon smoke test passed.\n" +
            $"Tribute: {basic.Definition.CardName}, Instance: " +
            $"{basic.InstanceId}\n" +
            $"Summoned: {advanced.Definition.CardName}, Instance: " +
            $"{advanced.InstanceId}\n" +
            "Tributes resolved before the summoned Monster occupied one " +
            "valid zone: True\n" +
            $"Graveyard cards: {player.Graveyard.Count}\n" +
            $"Available Energy: {player.AvailableEnergy}/" +
            $"{player.MaxEnergy}");
    }

    [MenuItem("Card Game/Diagnostics/Run Battle Rules Smoke Test")]
    public static void RunBattleRulesSmokeTest()
    {
        CardDefinition testMonster = CreateBattleTestMonster();
        GameConfig config = GameConfig.CreateDefault();
        List<CardDefinition> deck = new List<CardDefinition>();

        for (int index = 0; index < config.MinimumDeckSize; index++)
        {
            deck.Add(testMonster);
        }

        GameSession session = GameSession.Create(
            config,
            TestSeed,
            deck,
            deck);
        int firstPlayer = session.State.CurrentPlayerIndex;
        int secondPlayer = 1 - firstPlayer;
        PlayerState firstState = session.State.Players[firstPlayer];
        PlayerState secondState = session.State.Players[secondPlayer];

        StartTurnToMain(session, firstPlayer);
        CardInstance firstMonster = firstState.Hand[0];
        SummonAndCharge(session, firstPlayer, firstMonster);
        ApplyRequired(session, GameAction.AdvancePhase(firstPlayer));

        if (session.State.CurrentPhase != TurnPhase.End)
        {
            Debug.LogError("Turn 1 incorrectly entered Battle Phase.");
            return;
        }

        ActionResult forbiddenFirstTurnAttack = session.TryApply(
            GameAction.Attack(firstPlayer, firstMonster.InstanceId));

        if (forbiddenFirstTurnAttack.Succeeded)
        {
            Debug.LogError("A Monster incorrectly attacked on turn 1.");
            return;
        }

        ApplyRequired(session, GameAction.EndTurn(firstPlayer));

        StartTurnToMain(session, secondPlayer);
        CardInstance secondMonster = secondState.Hand[0];
        SummonAndCharge(session, secondPlayer, secondMonster);
        ApplyRequired(session, GameAction.AdvancePhase(secondPlayer));
        ApplyRequired(session, GameAction.Attack(
            secondPlayer,
            secondMonster.InstanceId,
            firstMonster.InstanceId));

        bool destructionIsCorrect =
            firstState.MonsterZones[0] == null &&
            firstState.Graveyard.Count == 1 &&
            firstState.Graveyard[0] == firstMonster &&
            firstState.Health == 3900;

        if (!destructionIsCorrect)
        {
            Debug.LogError(
                "Battle destruction or Energy penalty is incorrect.");
            return;
        }

        ApplyRequired(session, GameAction.AdvancePhase(secondPlayer));
        ApplyRequired(session, GameAction.EndTurn(secondPlayer));
        StartTurnToMain(session, firstPlayer);
        FinishTurn(session, firstPlayer);
        StartTurnToMain(session, secondPlayer);
        ApplyRequired(session, GameAction.AdvancePhase(secondPlayer));
        ApplyRequired(session, GameAction.Attack(
            secondPlayer,
            secondMonster.InstanceId));

        bool firstDirectAttackIsCorrect =
            firstState.Health == 2400 &&
            secondMonster.AttacksUsedThisTurn == 1 &&
            secondMonster.MaxAttacksThisTurn == 1;

        if (!firstDirectAttackIsCorrect)
        {
            Debug.LogError("Direct attack damage is incorrect.");
            return;
        }

        secondMonster.ModifyMaxAttacksThisTurn(1);
        ApplyRequired(session, GameAction.Attack(
            secondPlayer,
            secondMonster.InstanceId));

        if (firstState.Health != 900 ||
            secondMonster.AttacksUsedThisTurn != 2 ||
            secondMonster.MaxAttacksThisTurn != 2)
        {
            Debug.LogError("Additional attack allowance is incorrect.");
            return;
        }

        Debug.Log(
            "Battle rules smoke test passed.\n" +
            "Turn 1 skipped Battle Phase and attack was rejected: True\n" +
            "Attack Position defender took full ATK damage: True\n" +
            "Destroyed Monster moved to Graveyard: True\n" +
            "Destruction penalty: 100 HP for 1 allocated Energy\n" +
            "Attacker did not take counter-damage: True\n" +
            "Overflow damage did not reach player HP: True\n" +
            "Direct attack after opponent field became empty: 1500\n" +
            "Temporary maximum attacks increased from 1 to 2: True\n" +
            $"Defending player final HP: {firstState.Health}");
    }

    private static void ApplyRequired(
        GameSession session,
        GameAction action)
    {
        ActionResult result = session.TryApply(action);

        if (!result.Succeeded)
        {
            throw new System.InvalidOperationException(
                $"Required test action failed: {action}. " +
                result.ErrorMessage);
        }
    }

    private static void FinishTurn(GameSession session, int playerIndex)
    {
        if (session.State.CurrentPhase == TurnPhase.Standby)
        {
            ApplyRequired(session, GameAction.AdvancePhase(playerIndex));
        }

        if (session.State.CurrentPhase == TurnPhase.Main)
        {
            ApplyRequired(session, GameAction.AdvancePhase(playerIndex));
        }

        if (session.State.CurrentPhase == TurnPhase.Battle)
        {
            ApplyRequired(session, GameAction.AdvancePhase(playerIndex));
        }

        ApplyRequired(session, GameAction.EndTurn(playerIndex));
    }

    private static void StartTurnToMain(
        GameSession session,
        int playerIndex)
    {
        ApplyRequired(session, GameAction.ResolveDrawStep(playerIndex));
        ApplyRequired(
            session,
            GameAction.ResolveEnergyRefreshStep(playerIndex));

        if (session.State.CurrentPhase == TurnPhase.Standby)
        {
            ApplyRequired(session, GameAction.AdvancePhase(playerIndex));
        }
    }

    private static CardInstance FindHandCard(
        PlayerState player,
        int tributeCount)
    {
        for (int index = 0; index < player.Hand.Count; index++)
        {
            CardInstance card = player.Hand[index];

            if (card.Definition.IsMonster &&
                card.Definition.TributeCount == tributeCount)
            {
                return card;
            }
        }

        throw new System.InvalidOperationException(
            $"Test hand has no Monster requiring {tributeCount} tribute(s).");
    }

    private static void SummonAndCharge(
        GameSession session,
        int playerIndex,
        CardInstance monster)
    {
        ApplyRequired(session, GameAction.SummonMonster(
            playerIndex,
            monster.InstanceId,
            0,
            BattlePosition.Attack));
        ApplyRequired(session, GameAction.AllocateEnergy(
            playerIndex,
            monster.InstanceId,
            monster.Definition.AttackEnergyRequirement));
    }

    private static GameConfig CreateTestConfig(int startingHandSize)
    {
        GameConfig defaults = GameConfig.CreateDefault();
        return new GameConfig(
            defaults.StartingPlayerHealth,
            defaults.MinimumDeckSize,
            defaults.MaximumDeckSize,
            startingHandSize,
            defaults.CardsDrawnPerTurn,
            defaults.BaseMaximumEnergy,
            defaults.NormalSummonEnergyCost,
            defaults.MonsterZoneCount,
            defaults.SpellTrapZoneCount,
            defaults.DestructionPenaltyPerEnergy);
    }

    private static CardDefinition CreateBattleTestMonster()
    {
        return new CardDefinition(
            "diagnostic_battle_monster",
            "Diagnostic Battle Monster",
            CardCategory.Monster,
            "Used only in the in-memory Battle Rules smoke test.",
            MonsterAttribute.Earth,
            MonsterRank.Basic,
            MonsterType.Warrior,
            1000,
            1500,
            500,
            1,
            SpellType.Normal,
            TrapType.Normal,
            0,
            CardSpeed.Speed1);
    }

    private static List<CardDefinition> LoadDefinitions()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:CardData",
            new[] { "Assets/Data/Cards" });
        List<CardDefinition> definitions =
            new List<CardDefinition>(guids.Length);

        foreach (string guid in guids)
        {
            CardData cardData = AssetDatabase.LoadAssetAtPath<CardData>(
                AssetDatabase.GUIDToAssetPath(guid));

            if (cardData != null)
            {
                definitions.Add(CardDataConverter.ToDefinition(cardData));
            }
        }

        definitions.Sort((left, right) =>
            string.CompareOrdinal(left.CardId, right.CardId));
        return definitions;
    }

    private static List<CardDefinition> BuildTestDeck(
        IReadOnlyList<CardDefinition> availableCards,
        int deckSize,
        int offset)
    {
        List<CardDefinition> deck = new List<CardDefinition>(deckSize);

        for (int index = 0; index < deckSize; index++)
        {
            deck.Add(availableCards[(index + offset) % availableCards.Count]);
        }

        return deck;
    }

    private static string CreateOpeningSignature(GameState state)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(state.CurrentPlayerIndex);

        foreach (PlayerState player in state.Players)
        {
            builder.Append('|').Append(player.PlayerIndex);

            foreach (CardInstance card in player.Hand)
            {
                builder.Append(':')
                    .Append(card.InstanceId)
                    .Append('-')
                    .Append(card.Definition.CardId);
            }
        }

        return builder.ToString();
    }

    private static string DescribePlayer(PlayerState player)
    {
        StringBuilder handNames = new StringBuilder();

        for (int index = 0; index < player.Hand.Count; index++)
        {
            if (index > 0)
            {
                handNames.Append(", ");
            }

            handNames.Append(player.Hand[index].Definition.CardName);
        }

        return $"Player {player.PlayerIndex}: HP {player.Health}, Energy " +
            $"{player.AvailableEnergy}/{player.MaxEnergy}, Deck " +
            $"{player.Deck.Count}, Hand {player.Hand.Count} " +
            $"[{handNames}]";
    }
}
