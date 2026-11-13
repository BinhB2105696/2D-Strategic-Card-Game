using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Isolated fixtures only. Reflection is for arranging internal state, never
// exposed to UI/bots. The behavior under test runs through GameSession actions.
public static class HealthBalanceVerification
{
    private static int checks;

    [MenuItem("Card Game/Diagnostics/Verify November HP and KO Balance")]
    public static void Run()
    {
        checks = 0;
        VerifyAssets();
        VerifyHealth();
        VerifyRankPenalties();
        VerifyEffectAndExpiryKO();
        VerifyBattleTriggers();
        VerifyTribute();
        VerifyHealthText();
        Directory.CreateDirectory("Logs");
        string report = DateTime.UtcNow.ToString("o") + "\nPASS: " + checks +
            " checks: card data, HP cap/damage preservation, rank KO, effect/expiry KO, " +
            "battle-only triggers, tribute, draw result, current/maximum HP text and unchanged layout.\n";
        File.WriteAllText("Logs/HealthBalanceVerification.txt", report);
        Debug.Log(report);
    }

    private static void VerifyAssets()
    {
        string[] names = { "Dawnwing_Fairy", "Gravebound_Imp", "Sample_Tide_Sprite",
            "Sample_Stone_Guardian", "Fire_Dragon", "Sample_Sky_Sage", "Legendary_Fighter" };
        int[,] stats = { {700,400,150}, {800,500,200}, {600,450,200}, {1000,350,300},
            {1600,1000,300}, {2500,1600,600}, {3500,1000,800} };
        for (int i = 0; i < names.Length; i++)
        {
            CardData card = LoadMonster(names[i]);
            Require(card.BaseHealth == stats[i,0] && card.BaseAttack == stats[i,1] &&
                card.BaseDefense == stats[i,2], names[i] + " stats");
        }
        CardDefinition tide = CardDataConverter.ToDefinition(LoadMonster("Sample_Tide_Sprite"));
        Require(tide.Effects[0].Operations[0].Type == EffectOperationType.IncreaseMaximumHealth,
            "Tide is max HP, not healing");
        Require(CardDataConverter.ToDefinition(LoadMonster("Sample_Stone_Guardian"))
            .Effects[0].Operations[0].Amount == 100, "Stone +100 DEF");
        CardData trap = AssetDatabase.LoadAssetAtPath<CardData>("Assets/Data/Cards/Traps/Guardian_Shift.asset");
        Require(CardDataConverter.ToDefinition(trap).Effects[0].Operations[0].Amount == 500, "Guardian +500 DEF");
        Require(GameConfig.CreateDefault().StartingPlayerHealth == 6000, "6000 starting LP");
    }

    private static void VerifyHealth()
    {
        CardInstance card = new CardInstance(1, 0, Monster("hp", hp: 900));
        card.ReceiveDamage(300);
        card.ModifyMaximumHealth(300);
        Require(card.CurrentHealth == 900 && card.MaximumHealth == 1200, "600/900 -> 900/1200");
        card.ModifyMaximumHealth(-300);
        Require(card.CurrentHealth == 600 && card.MaximumHealth == 900, "reverse preserves damage");
        Require(card.RestoreHealth(1000) == 300 && card.CurrentHealth == 900, "healing cap and actual delta");
        Require(card.RestoreHealth(500) == 0, "full HP healing is zero");
        card.ModifyMaximumHealth(500);
        card.ReceiveDamage(1200);
        card.ModifyMaximumHealth(-500);
        Require(card.IsDestroyed && card.CurrentHealth == 0, "lost HP buff can KO");
        Require(card.RestoreHealth(500) == 0, "healing cannot revive KO");

        GameSession session = Session(CardDataConverter.ToDefinition(LoadMonster("Sample_Tide_Sprite")));
        CardInstance tide = Place(session, session.State.CurrentPlayerIndex, "sample_tide_sprite");
        tide.ReceiveDamage(300);
        Internal(tide, "SetBattlePosition", BattlePosition.Defense);
        ActionResult activation = Apply(session, GameAction.EnterEndPhase(tide.OwnerPlayerIndex));
        Require(tide.CurrentHealth == 500 && tide.MaximumHealth == 800, "authored Tide End Phase growth");
        Require(activation.Events.Any(e => e.Type == GameEventType.MonsterMaximumHealthModified && e.Amount == 200),
            "maximum HP event");
        Require(!session.TryApply(GameAction.ActivateCard(tide.OwnerPlayerIndex, tide.InstanceId)).Succeeded,
            "Tide no longer has a manual growth action");
    }

    private static void VerifyRankPenalties()
    {
        foreach (MonsterRank rank in Enum.GetValues(typeof(MonsterRank)))
        foreach (int energy in new[] {0, 7})
        {
            GameSession session = Session(Monster("victim", rank));
            CardInstance victim = Place(session, session.State.CurrentPlayerIndex, "victim");
            victim.AllocateEnergy(energy);
            victim.ReceiveDamage(10000);
            ActionResult result = Apply(session, GameAction.EnterEndPhase(victim.OwnerPlayerIndex));
            int expected = 6000 - 500 * ((int)rank + 1);
            Require(session.State.Players[victim.OwnerPlayerIndex].Health == expected, "rank " + rank + " E" + energy);
            Require(result.Events.Count(e => e.Type == GameEventType.MonsterDestroyed) == 1 &&
                victim.Zone == CardZone.Graveyard, "one KO and grave movement");
            Apply(session, GameAction.EndTurn(victim.OwnerPlayerIndex));
            Require(session.State.Players[victim.OwnerPlayerIndex].Health == expected, "no double KO penalty");
        }
    }

    private static void VerifyEffectAndExpiryKO()
    {
        EffectDefinition reduction = Effect(EffectTriggerType.ManualActivation,
            EffectOperationType.ReduceMaximumHealth, 1000, EffectTargetType.OpponentMonster);
        CardDefinition imp = CardDataConverter.ToDefinition(LoadMonster("Gravebound_Imp"));
        GameSession session = Session(Monster("caster", effect: reduction), imp);
        int actor = session.State.CurrentPlayerIndex;
        CardInstance caster = Place(session, actor, "caster");
        CardInstance victim = Place(session, 1 - actor, "gravebound_imp");
        ActionResult result = Apply(session, GameAction.ActivateCard(actor, caster.InstanceId,
            targetInstanceId: victim.InstanceId));
        Require(victim.Zone == CardZone.Graveyard && session.State.Players[1-actor].Health == 5500,
            "max HP reduction effect KOs and penalizes");
        Require(!session.State.HasPendingTriggeredEffect, "effect KO does not trigger battle-only Imp");

        session = Session(Monster("expiry", hp: 900));
        actor = session.State.CurrentPlayerIndex;
        victim = Place(session, actor, "expiry");
        Internal(session.State, "ApplyStatModifier", victim.InstanceId, victim, 0, 0, 1, 500);
        victim.ReceiveDamage(1200);
        Apply(session, GameAction.EnterEndPhase(actor));
        result = Apply(session, GameAction.EndTurn(actor));
        Require(victim.Zone == CardZone.Graveyard && session.State.Players[actor].Health == 5500,
            "expired max HP buff KO");
        Require(result.Events.Any(e => e.Type == GameEventType.MonsterMaximumHealthModified && e.Amount == -500),
            "expiry emits max HP delta");

        session = Session(Monster("draw", hp: 900));
        actor = session.State.CurrentPlayerIndex;
        foreach (PlayerState owner in session.State.Players)
        {
            victim = Place(session, owner.PlayerIndex, "draw");
            Internal(session.State, "ApplyStatModifier", victim.InstanceId, victim, 0, 0, 1, 500);
            victim.ReceiveDamage(1200);
            owner.TakeDamage(5500);
        }
        Apply(session, GameAction.EnterEndPhase(actor));
        result = Apply(session, GameAction.EndTurn(actor));
        Require(session.State.IsFinished && session.State.WinnerPlayerIndex == -1, "simultaneous KO gives draw");
        Require(result.Events.Count(e => e.Type == GameEventType.MatchEnded) == 1 &&
            !result.Events.Any(e => e.Type == GameEventType.TurnStarted), "finish once before next turn");
    }

    private static void VerifyBattleTriggers()
    {
        CardDefinition fighterDef = CardDataConverter.ToDefinition(LoadMonster("Legendary_Fighter"));
        CardDefinition impDef = CardDataConverter.ToDefinition(LoadMonster("Gravebound_Imp"));
        GameSession session = Session(fighterDef, impDef);
        // The first turn cannot enter Battle; advance through End normally.
        int firstPlayer = session.State.CurrentPlayerIndex;
        Apply(session, GameAction.EnterEndPhase(firstPlayer));
        Apply(session, GameAction.EndTurn(firstPlayer));
        int actor = session.State.CurrentPlayerIndex;
        CardInstance fighter = Place(session, actor, "legendary_fighter");
        CardInstance imp = Place(session, 1-actor, "gravebound_imp");
        fighter.AllocateEnergy(2);
        fighter.ReceiveDamage(300);
        Internal(session.State, "SetCurrentPhase", TurnPhase.Battle);
        Apply(session, GameAction.Attack(actor, fighter.InstanceId, imp.InstanceId));
        Require(session.State.PendingTriggeredEffect.SourceInstanceId == fighter.InstanceId && fighter.AllocatedEnergy == 2,
            "Fighter recovery waits for its owner's choice");
        ActionResult result = Apply(session, GameAction.ResolveTriggeredEffect(actor, fighter.InstanceId, fighter.InstanceId));
        Require(fighter.CurrentHealth == 3500 && fighter.AllocatedEnergy == 0, "Fighter heals to cap and spends ALL Energy");
        Require(result.Events.Any(e => e.Type == GameEventType.MonsterHealthModified && e.Amount == 300),
            "heal animation gets actual 300, not attempted 1000");
        Require(session.State.HasPendingTriggeredEffect, "battle KO still offers Imp recovery");
        Apply(session, GameAction.ResolveTriggeredEffect(1-actor, imp.InstanceId, imp.InstanceId));
        Require(imp.Zone == CardZone.Hand && imp.CurrentHealth == imp.MaximumHealth && imp.MaximumHealth == 800,
            "recovered card has base full HP");
    }

    private static void VerifyTribute()
    {
        GameSession session = Session(Monster("basic"), Monster("advanced", MonsterRank.Advanced));
        int actor = session.State.CurrentPlayerIndex;
        CardInstance tribute = Place(session, actor, "basic");
        tribute.ModifyMaximumHealth(300); tribute.AllocateEnergy(3);
        CardInstance summon = session.State.Players[actor].Hand.First(c => c.Definition.CardId == "advanced");
        ActionResult result = Apply(session, GameAction.SummonMonster(actor, summon.InstanceId, 0,
            BattlePosition.Attack, new[] {tribute.InstanceId}));
        Require(session.State.Players[actor].Health == 6000 &&
            !result.Events.Any(e => e.Type == GameEventType.MonsterDestroyed), "tribute is not KO");
        Require(summon.CurrentHealth == summon.Definition.BaseHealth && summon.AllocatedEnergy == 0,
            "tribute HP/Energy not inherited");
    }

    private static void VerifyHealthText()
    {
        GameObject zone = new GameObject("VerificationZone", typeof(RectTransform));
        try
        {
            var view = zone.AddComponent<DuelMonsterZoneStatusView>();
            view.Initialize(AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/TMP/Cinzel-Bold SDF.asset"));
            var card = new CardInstance(1, 0, Monster("health_text", hp: 900));
            card.ReceiveDamage(300); view.SetMonster(card);
            var label = zone.transform.Find("RuntimeMonsterStatus/HealthText").GetComponent<TMPro.TMP_Text>();
            Require(label.text == "HP 600", "current HP text 600");
            var bar = zone.transform.Find("RuntimeMonsterStatus/HealthBar").GetComponent<DuelMonsterHealthBar>();
            Require(Mathf.Approximately(bar.Fraction, 600f / 900f), "HP bar damage ratio");
            card.ModifyMaximumHealth(300); view.SetMonster(card);
            Require(label.text == "HP 900", "current HP text 900");
            Require(Mathf.Approximately(bar.Fraction, 0.75f), "HP bar maximum change ratio");
            Require(label.fontSize == 35 && label.characterSpacing == 5 &&
                label.alignment == TMPro.TextAlignmentOptions.Right && !label.enableAutoSizing &&
                label.rectTransform.sizeDelta == new Vector2(-12, 40) &&
                label.rectTransform.anchoredPosition == new Vector2(0, 25), "right-aligned HP layout");
        }
        finally { UnityEngine.Object.DestroyImmediate(zone); }
    }

    private static CardData LoadMonster(string name) => AssetDatabase.LoadAssetAtPath<CardData>(
        "Assets/Data/Cards/Monsters/" + name + ".asset");
    private static CardDefinition Monster(string id, MonsterRank rank = MonsterRank.Basic, int hp = 1000,
        EffectDefinition effect = null) => new CardDefinition(id, id, CardCategory.Monster, "",
            MonsterAttribute.Wood, rank, MonsterType.Fairy, hp, 1000, 200, 1,
            SpellType.Normal, TrapType.Normal, (int)rank, CardSpeed.Speed1,
            effect == null ? null : new[] {effect});
    private static EffectDefinition Effect(EffectTriggerType trigger, EffectOperationType operation,
        int amount, EffectTargetType target) => new EffectDefinition("fixture", trigger,
            EffectKind.Activated, EffectPhaseMask.Main, EffectTurnScope.YourTurn,
            EffectActivationWindow.OpenGameState, EffectActivationSource.Standard, null, null,
            target, target == EffectTargetType.None ? 0 : 1,
            new[] {new EffectOperationDefinition(operation, amount)});
    private static GameSession Session(params CardDefinition[] deck)
    {
        var config = new GameConfig(6000, deck.Length, 40, deck.Length, 1, 5, 1, 3, 3, 500);
        GameSession session = GameSession.Create(config, 20261103, deck, deck);
        Internal(session.State, "SetCurrentPhase", TurnPhase.Main);
        return session;
    }
    private static CardInstance Place(GameSession session, int owner, string id)
    {
        PlayerState player = session.State.Players[owner];
        CardInstance card = player.Hand.First(c => c.Definition.CardId == id);
        Internal(player, "PlaceMonsterFromHand", card, 0, BattlePosition.Attack);
        return card;
    }
    private static ActionResult Apply(GameSession session, GameAction action)
    {
        ActionResult result = session.TryApply(action);
        Require(result.Succeeded, result.ErrorMessage);
        return result;
    }
    private static void Internal(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("HP/KO verification failed: " + message);
        checks++;
    }
}
