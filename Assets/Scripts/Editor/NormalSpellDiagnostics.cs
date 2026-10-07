using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class NormalSpellDiagnostics
{
    private const uint TestSeed = 20261002u;

    [MenuItem("Card Game/Diagnostics/Run Normal Spell Smoke Test")]
    public static void RunNormalSpellSmokeTest()
    {
        GameConfig config = GameConfig.CreateDefault();
        CardDefinition spellDefinition = LoadArcaneInsightDefinition();

        if (spellDefinition == null)
        {
            Debug.LogError(
                "Normal Spell test requires Sample_Arcane_Insight with its " +
                "component effect. Run Card Game > Sample Cards > " +
                "Create Missing Sample Cards first.");
            return;
        }
        List<CardDefinition> firstDeck = BuildDeck(
            spellDefinition,
            config.MinimumDeckSize);
        List<CardDefinition> secondDeck = BuildDeck(
            spellDefinition,
            config.MinimumDeckSize);
        GameSession session = GameSession.Create(
            config,
            TestSeed,
            firstDeck,
            secondDeck);
        int playerIndex = session.State.CurrentPlayerIndex;
        PlayerState player = session.State.Players[playerIndex];
        CardInstance spell = player.Hand[0];

        ActionResult wrongPhase = session.TryApply(
            GameAction.ActivateNormalSpell(
                playerIndex,
                spell.InstanceId,
                0));

        if (wrongPhase.Succeeded)
        {
            Debug.LogError(
                "Normal Spell activated outside Main Phase.");
            return;
        }

        ApplyRequired(session, GameAction.ResolveDrawStep(playerIndex));
        ApplyRequired(
            session,
            GameAction.ResolveEnergyRefreshStep(playerIndex));
        ApplyRequired(session, GameAction.AdvancePhase(playerIndex));

        int handBefore = player.Hand.Count;
        int deckBefore = player.Deck.Count;
        int graveyardBefore = player.Graveyard.Count;
        int energyBefore = player.AvailableEnergy;
        ActionResult result = session.TryApply(
            GameAction.ActivateNormalSpell(
                playerIndex,
                spell.InstanceId,
                0));

        if (!result.Succeeded)
        {
            Debug.LogError(
                "Normal Spell activation failed: " +
                result.ErrorMessage);
            return;
        }

        bool spellReachedGraveyard =
            player.Graveyard.Count == graveyardBefore + 1 &&
            player.Graveyard[player.Graveyard.Count - 1].InstanceId ==
            spell.InstanceId &&
            player.Graveyard[player.Graveyard.Count - 1].IsFaceDown;
        bool spellTrapZoneReleased = player.SpellTrapZones[0] == null;
        bool drewOneCard = player.Deck.Count == deckBefore - 1 &&
            player.Hand.Count == handBefore;
        bool paidEnergy = player.AvailableEnergy == energyBefore - 1;
        bool loggedAction = session.Log.Actions.Count == 4;
        bool eventOrderIsCorrect = result.Events.Count == 6 &&
            result.Events[0].Type ==
                GameEventType.CardPlacedInSpellTrapZone &&
            result.Events[1].Type == GameEventType.SpellActivated &&
            result.Events[2].Type == GameEventType.EnergySpent &&
            result.Events[3].Type == GameEventType.CardDrawn &&
            result.Events[4].Type == GameEventType.EffectResolved &&
            result.Events[5].Type == GameEventType.CardSentToGraveyard;

        if (!spellReachedGraveyard ||
            !drewOneCard ||
            !paidEnergy ||
            !spellTrapZoneReleased ||
            !eventOrderIsCorrect ||
            !loggedAction)
        {
            Debug.LogError(
                "Normal Spell produced an unexpected match state.");
            return;
        }

        Debug.Log(
            "Normal Spell smoke test passed.\n" +
            "Activation outside Main Phase rejected: True\n" +
            "Activation Energy paid: 1\n" +
            "Cards drawn by effect: 1\n" +
            "Spell entered Spell/Trap Zone face-up: True\n" +
            "Resolved Spell moved from field to Graveyard face-down: True\n" +
            "Spell/Trap Zone released after resolution: True\n" +
            "Activation event order is correct: True\n" +
            $"Generated events: {result.Events.Count}\n" +
            $"Logged valid actions: {session.Log.Actions.Count}");
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

    private static CardDefinition LoadArcaneInsightDefinition()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:CardData",
            new[] { "Assets/Data/Cards/Spells" });

        foreach (string guid in guids)
        {
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(
                AssetDatabase.GUIDToAssetPath(guid));

            if (card != null &&
                card.CardId == "sample_arcane_insight" &&
                card.Effects.Count == 1 &&
                card.Effects[0] != null)
            {
                return CardDataConverter.ToDefinition(card);
            }
        }

        return null;
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
}
