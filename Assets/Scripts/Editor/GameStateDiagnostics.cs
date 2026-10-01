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

        ApplyRequired(session, GameAction.ResolveDrawAndEnergy(firstPlayer));
        ApplyRequired(session, GameAction.AdvancePhase(firstPlayer));
        ApplyRequired(session, GameAction.AdvancePhase(firstPlayer));
        ApplyRequired(session, GameAction.EndTurn(firstPlayer));
        ApplyRequired(session, GameAction.ResolveDrawAndEnergy(secondPlayer));

        bool stateIsCorrect =
            session.State.TurnNumber == 2 &&
            session.State.CurrentPlayerIndex == secondPlayer &&
            session.State.CurrentPhase == TurnPhase.Main &&
            session.State.Players[firstPlayer].Hand.Count == 5 &&
            session.State.Players[secondPlayer].Hand.Count == 5 &&
            session.Log.Actions.Count == 5;

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
            "Flow: Draw & Energy -> Main -> Battle -> End -> next turn");
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
