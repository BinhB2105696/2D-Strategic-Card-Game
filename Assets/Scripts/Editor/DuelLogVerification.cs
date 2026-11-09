#if UNITY_EDITOR || DUEL_LOG_HEADLESS
using System;
using System.Collections.Generic;

public static class DuelLogVerification
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Card Game/Diagnostics/Verify Duel Log")]
#endif
    public static void Verify()
    {
        VerifyPrivacy();
        VerifyHealth();
#if UNITY_EDITOR
        UnityEngine.Debug.Log("Duel Log PASS: private draws/sets, reveal history, chain order, maximum HP and capture without duplicate entries.");
#else
        Console.WriteLine("Duel Log PASS: private draws/sets, reveal history, chain order, maximum HP and capture without duplicate entries.");
#endif
    }

    private static GameState Fixture()
    {
        var card = new CardDefinition("private_card", "Secret Card", CardCategory.Monster, "",
            MonsterAttribute.Wood, MonsterRank.Basic, MonsterType.Fairy, 1000, 500, 300, 1,
            SpellType.Normal, TrapType.Normal, 0, CardSpeed.Speed1);
        var deck = new List<CardDefinition>();
        for (int i = 0; i < 30; i++) deck.Add(card);
        return GameState.CreateNewMatch(GameConfig.CreateDefault(), 19, deck, deck);
    }

    private static void VerifyPrivacy()
    {
        GameState state = Fixture();
        var log = new DuelLogModel(state);
        CardInstance card = state.Players[1].Hand[0];
        log.Record(GameAction.ResolveDrawStep(1), new[] { GameEvent.CardDrawn(1, card.InstanceId) }, state);
        Require(!Join(log, 0).Contains("Secret Card"), "opponent draw hidden");
        Require(Join(log, 1).Contains("Secret Card"), "own draw named");
        log.Record(GameAction.SetTrap(1, card.InstanceId, 0), new[] {
            GameEvent.CardPlacedInSpellTrapZone(1, card.InstanceId, 0, TurnPhase.Main),
            GameEvent.CardSet(1, card.InstanceId, 0) }, state);
        string[] oldLines = new List<string>(log.Lines(0)).ToArray();
        Require(!Join(log, 0).Contains("Secret Card"), "placement and set hidden");
        log.Record(GameAction.PassResponse(1), new[] {
            GameEvent.TrapActivated(1, card.InstanceId, TurnPhase.Battle),
            GameEvent.ChainLinkAdded(1, card.InstanceId, 2, TurnPhase.Battle),
            GameEvent.EffectResolved(1, card.InstanceId, TurnPhase.Battle) }, state);
        Require(Join(log, 0).Contains("Secret Card"), "activated card public");
        for (int i = 0; i < oldLines.Length; i++) Require(oldLines[i] == log.Lines(0)[i], "past hidden lines remain frozen");
        string text = Join(log, 0);
        Require(text.IndexOf("Chain Link 2", StringComparison.Ordinal) < text.IndexOf("effect resolved", StringComparison.Ordinal), "event order retained");
        int count = log.Lines(0).Count;
        log.Capture(state); log.Capture(state);
        Require(log.Lines(0).Count == count, "render/capture does not duplicate journal");
    }

    private static void VerifyHealth()
    {
        GameState state = Fixture();
        CardInstance card = state.Players[0].Hand[0];
        card.ReceiveDamage(400);
        var log = new DuelLogModel(state);
        card.ModifyMaximumHealth(300);
        log.Record(GameAction.PassResponse(0), new[] {
            GameEvent.MonsterMaximumHealthModified(0, card.InstanceId, 300, TurnPhase.Main),
            GameEvent.MonsterHealthModified(0, card.InstanceId, 300, TurnPhase.Main) }, state);
        string text = Join(log, 0);
        Require(text.Contains("maximum HP 1000 → 1300"), "maximum HP change");
        Require(text.Contains("HP 600 → 900"), "maximum change does not double-count current HP");
    }

    private static string Join(DuelLogModel log, int viewer) => string.Join("\n", log.Lines(viewer));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Duel Log: " + message);
    }
}
#endif
