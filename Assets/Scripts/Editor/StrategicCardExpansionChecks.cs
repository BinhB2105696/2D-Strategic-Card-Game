using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class StrategicCardExpansionChecks
{
    private static int checks;
    private static Dictionary<string, CardDefinition> cards;

    public static int Run()
    {
        checks = 0; cards = StrategicCardExpansionCatalog.Create().ToDictionary(card => card.CardId);
        Check(cards.Count == 24 && cards.Values.All(card => card.Effects.All(effect => effect.MaxUses == 0)), "24 cards without usage caps");
        Check(cards.Values.Count(card => card.CopyLimitOverride == 2) == 2, "only two extra support cards have a copy override");
        Check(cards.Values.All(card => !card.EffectText.Contains("once per turn")), "new English text has no added per-turn caps");
        Energy(); TargetCosts(); Searches(); AuraAndPriority(); Combat(); EndEffects(); CopyLimits(); BotSmoke();
        return checks;
    }

#if !UNITY_EDITOR
    public static void Main()
    {
        int baseline = ExistingCardBalanceChecks.Run();
        Console.WriteLine("Existing balance PASS: " + baseline + " checks.");
        Console.WriteLine("Strategic expansion PASS: " + Run() + " checks.");
    }
#endif

    private static CardDefinition Unit(string id, int hp = 2000, int attack = 500, int defense = 300)
        => new CardDefinition(id, id, CardCategory.Monster, "", MonsterAttribute.Wood, MonsterRank.Basic,
            MonsterType.Warrior, hp, attack, defense, 1, SpellType.Normal, TrapType.Normal, 0, CardSpeed.Speed1);

    private static GameState State(CardDefinition filler = null)
    {
        var deck = Enumerable.Repeat(filler ?? Unit("test.unit"), 30).ToArray();
        GameState state = GameState.CreateNewMatch(GameConfig.CreateDefault(), 77, deck, deck);
        Call(state, "StartNextTurn"); Call(state, "SetCurrentPhase", TurnPhase.Main);
        return state;
    }

    private static CardInstance Place(GameState state, int owner, int zone, long id, CardDefinition definition,
        BattlePosition position = BattlePosition.Attack, int energy = 0)
    {
        var card = new CardInstance(id, owner, definition);
        Call(state.Players[owner], "PlaceMonsterFromHand", card, zone, position);
        card.AllocateEnergy(energy); card.ResetTurnFlags();
        return card;
    }

    private static CardInstance DeckCard(GameState state, int owner, long id, CardDefinition definition)
    {
        var card = new CardInstance(id, owner, definition);
        var deck = (List<CardInstance>)typeof(PlayerState).GetProperty("MutableDeck", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state.Players[owner]);
        deck.Add(card); return card;
    }

    private static GameAction Activation(GameState state, long source, long target = 0)
        => GameRules.GetLegalActions(state, state.InteractionPlayerIndex).First(action => action.Type == GameActionType.ActivateCard &&
            action.SourceInstanceId == source && (target == 0 || action.TargetInstanceId == target));

    private static void MainSnapshot(GameState state)
    { Call(state, "SetCurrentPhase", TurnPhase.Standby); Call(state, "SetCurrentPhase", TurnPhase.Main); }

    private static void Energy()
    {
        GameState state = State(cards["energy_reserve"]); int actor = state.CurrentPlayerIndex;
        PlayerState owner = state.Players[actor]; owner.TrySpendEnergy(3);
        Apply(state, Activation(state, owner.Hand[0].InstanceId));
        Check(owner.AvailableEnergy == 3, "Reserve restores one when LP are tied");
        owner.TakeDamage(100);
        Apply(state, Activation(state, owner.Hand[0].InstanceId));
        Check(owner.AvailableEnergy == 5, "Reserve adds one bonus when behind in LP");
        Check(!GameRules.GetLegalActions(state, actor).Any(action => action.Type == GameActionType.ActivateCard), "full pool cannot activate Reserve");
        owner.TrySpendEnergy(1);
        ActionResult capped = Apply(state, Activation(state, owner.Hand[0].InstanceId));
        Check(owner.AvailableEnergy == 5 && capped.Events.Single(e => e.Type == GameEventType.EnergyRestored).Amount == 1,
            "Energy cap and log report actual restoration");

        state = State(cards["emergency_reinforcements"]); actor = state.CurrentPlayerIndex; owner = state.Players[actor];
        Place(state, 1 - actor, 0, 2001, Unit("enemy")); MainSnapshot(state); owner.TrySpendEnergy(4);
        Place(state, actor, 0, 1001, Unit("own")); Place(state, actor, 1, 1002, Unit("own2"));
        Apply(state, Activation(state, owner.Hand[0].InstanceId));
        Check(owner.AvailableEnergy == 3, "comeback uses start-of-Main counts even after summoning");
        state = State(cards["emergency_reinforcements"]); actor = state.CurrentPlayerIndex;
        state.Players[actor].TrySpendEnergy(2);
        Check(!GameRules.GetLegalActions(state, actor).Any(action => action.Type == GameActionType.ActivateCard), "tied start counts do not enable comeback");

        state = State(cards["energy_reallocation"]); actor = state.CurrentPlayerIndex; owner = state.Players[actor];
        CardInstance battery = Place(state, actor, 0, 1001, Unit("battery"), energy: 2); owner.TrySpendEnergy(2);
        Apply(state, Activation(state, owner.Hand[0].InstanceId, battery.InstanceId));
        Check(owner.AvailableEnergy == 4 && battery.AllocatedEnergy == 1, "reallocation conserves Energy between pools");
    }

    private static void TargetCosts()
    {
        GameState state = State(cards["battlefield_medicine"]); int actor = state.CurrentPlayerIndex;
        CardInstance target = Place(state, actor, 0, 1001, Unit("wounded")); target.ReceiveDamage(200);
        state.Players[actor].TrySpendEnergy(5);
        Apply(state, Activation(state, state.Players[actor].Hand[0].InstanceId, target.InstanceId));
        Check(target.CurrentHealth == target.MaximumHealth && state.Players[actor].AvailableEnergy == 0, "free medicine heals only to cap");

        state = State(); actor = state.CurrentPlayerIndex;
        CardInstance medic = Place(state, actor, 0, 1001, cards["field_medic"], energy: 2);
        target = Place(state, actor, 1, 1002, Unit("patient")); target.ReceiveDamage(900);
        Apply(state, Activation(state, medic.InstanceId, target.InstanceId));
        Apply(state, Activation(state, medic.InstanceId, target.InstanceId));
        Check(target.CurrentHealth == 1900 && medic.AllocatedEnergy == 0 && state.Players[actor].AvailableEnergy == 5,
            "Medic repeats while stored Energy lasts, without available Energy cost");
        Check(!GameRules.GetLegalActions(state, actor).Any(a => a.Type == GameActionType.ActivateCard && a.SourceInstanceId == medic.InstanceId), "Medic cannot spend nonexistent Energy");

        state = State(); actor = state.CurrentPlayerIndex;
        CardInstance leech = Place(state, actor, 0, 1001, cards["static_leech"], energy: 2);
        CardInstance enemy = Place(state, 1 - actor, 0, 2001, Unit("charged"), energy: 2);
        Apply(state, Activation(state, leech.InstanceId, enemy.InstanceId));
        Check(leech.AllocatedEnergy == 1 && enemy.AllocatedEnergy == 1 && state.Players[actor].AvailableEnergy == 5,
            "Leech removes Energy from both monsters without refund");

        state = State(cards["change_of_plans"]); actor = state.CurrentPlayerIndex;
        PlayerState owner = state.Players[actor]; int before = owner.Hand.Count;
        long discarded = owner.Hand[1].InstanceId;
        Apply(state, Activation(state, owner.Hand[0].InstanceId, discarded));
        Check(owner.Hand.Count == before && owner.Graveyard.Any(card => card.InstanceId == discarded) && owner.Health == 6000,
            "discard-one draw-two preserves hand count and has no KO penalty");
        Check(owner.AvailableEnergy == 5, "discard cost uses no Energy");
    }

    private static void Searches()
    {
        GameState state = State(cards["elite_summons"]); int actor = state.CurrentPlayerIndex;
        CardInstance boss = DeckCard(state, actor, 1001, cards["aegis_sentinel"]);
        PlayerState owner = state.Players[actor]; owner.TakeDamage(1600); MainSnapshot(state); owner.TrySpendEnergy(5);
        ActionResult search = Apply(state, Activation(state, owner.Hand[0].InstanceId, boss.InstanceId));
        Check(boss.Zone == CardZone.Hand && owner.AvailableEnergy == 0, "Elite search can be free using start-of-Main LP deficit");
        Check(search.Events.Any(e => e.Type == GameEventType.CardRevealed && e.CardInstanceId == boss.InstanceId) &&
            search.Events.Any(e => e.Type == GameEventType.DeckShuffled), "search reveals chosen card and shuffles");

        state = State(); actor = state.CurrentPlayerIndex;
        CardInstance archivist = Place(state, actor, 0, 1001, cards["arcane_archivist"]);
        CardInstance spell = DeckCard(state, actor, 1101, cards["stop_defense"]);
        CardInstance other = DeckCard(state, actor, 1102, cards["energy_reserve"]);
        BotGameStateView view = BotGameStateView.Create(state, actor);
        Check(view.ChoiceCards.All(card => card.OwnerPlayerIndex == actor) && view.FindCard(spell.InstanceId).IsKnown,
            "only legal owned search choices reveal definitions to policy");
        Check(view.Players[1 - actor].Hand.All(card => !card.IsKnown), "opponent private hand remains masked");
        Apply(state, Activation(state, archivist.InstanceId, spell.InstanceId));
        Apply(state, Activation(state, archivist.InstanceId, other.InstanceId));
        Check(spell.Zone == CardZone.Hand && other.Zone == CardZone.Hand && state.Players[actor].AvailableEnergy == 3,
            "Archivist can search repeatedly for one Energy each");

        state = State(cards["recover_the_fallen"]); actor = state.CurrentPlayerIndex;
        CardInstance fallen = Place(state, actor, 0, 1001, Unit("fallen"));
        Call(state.Players[actor], "MoveMonsterToGraveyard", 0);
        Apply(state, Activation(state, state.Players[actor].Hand[0].InstanceId, fallen.InstanceId));
        Check(fallen.Zone == CardZone.Hand, "recovery Spell selects Basic in graveyard");

        state = State(cards["veil_strategist"]); actor = state.CurrentPlayerIndex;
        var trap = new CardDefinition("normal.trap", "Normal Trap", CardCategory.Trap, "", MonsterAttribute.Wood,
            MonsterRank.Basic, MonsterType.Warrior, 0, 0, 0, 0, SpellType.Normal, TrapType.Normal, 0, CardSpeed.Speed2);
        CardInstance trapInstance = DeckCard(state, actor, 2001, trap);
        Call(state.Players[actor], "MoveDeckCardToHand", trapInstance.InstanceId);
        Call(state.Players[actor], "PlaceSpellTrapFromHand", trapInstance, 0, false);
        Call(state.Players[actor], "MoveSpellTrapToGraveyard", 0);
        Place(state, actor, 0, 1001, Unit("tribute"));
        GameAction summon = GameRules.GetLegalActions(state, actor).First(a => a.Type == GameActionType.SummonMonster);
        Apply(state, summon);
        Check(state.HasPendingTriggeredEffect && state.PendingTriggeredEffect.ContainsCandidate(trapInstance.InstanceId),
            "Veil offers Normal Trap from graveyard");
        Apply(state, GameAction.ResolveTriggeredEffect(actor, summon.SourceInstanceId, trapInstance.InstanceId));
        Check(trapInstance.Zone == CardZone.Hand, "Veil recovers to hand, not ready-to-activate field");
    }

    private static void AuraAndPriority()
    {
        GameState state = State(cards["stop_defense"]); int actor = state.CurrentPlayerIndex;
        CardInstance first = Place(state, actor, 0, 1001, cards["battle_herald"]);
        CardInstance second = Place(state, actor, 1, 1002, cards["battle_herald"]);
        CardInstance own = Place(state, actor, 2, 1003, Unit("fighter"));
        CardInstance guard = Place(state, 1 - actor, 0, 2001, cards["aegis_sentinel"], BattlePosition.Defense);
        CardInstance guard2 = Place(state, 1 - actor, 1, 2002, cards["aegis_sentinel"]);
        CardInstance safe = Place(state, 1 - actor, 2, 2003, Unit("safe"));
        Apply(state, GameAction.AllocateEnergy(actor, own.InstanceId, 1));
        Check(first.CurrentAttack == 750 && second.CurrentAttack == 750 && own.CurrentAttack == 900, "two attack auras stack and include sources");
        Apply(state, GameAction.AllocateEnergy(actor, own.InstanceId, 1));
        Check(own.CurrentAttack == 900, "refreshing auras does not accumulate duplicates");
        Apply(state, Activation(state, state.Players[actor].Hand[0].InstanceId, guard.InstanceId));
        Check(guard.BattlePosition == BattlePosition.Attack && guard.AttackPriority == 1, "Stop Defense changes pose, not priority");
        Apply(state, GameAction.AdvancePhase(actor));
        var attacks = GameRules.GetLegalActions(state, actor).Where(a => a.Type == GameActionType.Attack && a.SourceInstanceId == own.InstanceId).ToArray();
        Check(attacks.Any(a => a.TargetInstanceId == guard.InstanceId) && attacks.Any(a => a.TargetInstanceId == guard2.InstanceId) &&
            attacks.All(a => a.TargetInstanceId != safe.InstanceId), "both equal-priority guards can be attacked in any pose");
        Call(state.Players[actor], "MoveMonsterToGraveyard", 0);
        Apply(state, GameAction.EnterEndPhase(actor));
        Check(own.CurrentAttack == 700, "lost aura source removes exactly its contribution");

        state = State(cards["stop_defense"]); actor = state.CurrentPlayerIndex;
        CardInstance keeper = Place(state, 1 - actor, 0, 2001, cards["bastion_keeper"], BattlePosition.Defense);
        CardInstance defended = Place(state, 1 - actor, 1, 2002, Unit("defended"), BattlePosition.Defense);
        Apply(state, Activation(state, state.Players[actor].Hand[0].InstanceId, defended.InstanceId));
        Check(defended.CurrentDefense == 300 && keeper.CurrentDefense == 500, "Defense aura follows target pose without disabling its source");
    }

    private static void Combat()
    {
        GameState state = State(); int actor = state.CurrentPlayerIndex;
        CardInstance attacker = Place(state, actor, 0, 1001, cards["bloodfang_raider"], energy: 1);
        attacker.ReceiveDamage(400);
        CardInstance victim = Place(state, 1 - actor, 0, 2001, Unit("small", 100));
        Call(state, "SetCurrentPhase", TurnPhase.Battle);
        Apply(state, GameAction.Attack(actor, attacker.InstanceId, victim.InstanceId));
        Check(attacker.CurrentHealth == 550, "lifesteal uses 100 actual HP loss, not 650 overkill");

        state = State(); actor = state.CurrentPlayerIndex;
        attacker = Place(state, actor, 0, 1001, cards["bloodfang_raider"], energy: 1); attacker.ReceiveDamage(800);
        victim = Place(state, 1 - actor, 0, 2001, cards["thorn_bastion"], BattlePosition.Defense);
        Call(state, "SetCurrentPhase", TurnPhase.Battle);
        Apply(state, GameAction.Attack(actor, attacker.InstanceId, victim.InstanceId));
        Check(attacker.CurrentHealth == 75 && victim.CurrentHealth == 1550, "reflection happens once and surviving attacker steals only actual damage");

        state = State(); actor = state.CurrentPlayerIndex;
        attacker = Place(state, actor, 0, 1001, cards["bloodfang_raider"], energy: 1); attacker.ReceiveDamage(875);
        victim = Place(state, 1 - actor, 0, 2001, cards["thorn_bastion"], BattlePosition.Defense);
        Call(state, "SetCurrentPhase", TurnPhase.Battle);
        Apply(state, GameAction.Attack(actor, attacker.InstanceId, victim.InstanceId));
        Check(attacker.Zone == CardZone.Graveyard, "lethal reflected damage cannot be undone by lifesteal");

        state = State(); actor = state.CurrentPlayerIndex;
        attacker = Place(state, actor, 0, 1001, cards["siegebreaker"], energy: 2);
        victim = Place(state, 1 - actor, 0, 2001, Unit("wall", 2000, defense: 700), BattlePosition.Defense);
        Call(state, "SetCurrentPhase", TurnPhase.Battle);
        Apply(state, GameAction.Attack(actor, attacker.InstanceId, victim.InstanceId));
        Check(victim.CurrentHealth == 1350 && victim.CurrentDefense == 700, "breach ignores 300 DEF for this battle without changing printed/current DEF");

        state = State(); actor = state.CurrentPlayerIndex;
        attacker = Place(state, actor, 0, 1001, cards["ambush_lancer"], energy: 2);
        Call(state, "SetCurrentPhase", TurnPhase.Battle);
        Apply(state, GameAction.Attack(actor, attacker.InstanceId));
        Check(state.HasPendingTriggeredEffect && attacker.AllocatedEnergy == 2, "direct attack offers optional ambush");
        Apply(state, GameAction.ResolveTriggeredEffect(actor, attacker.InstanceId, attacker.InstanceId));
        Check(attacker.BattlePosition == BattlePosition.Defense && attacker.AllocatedEnergy == 0 && attacker.AttacksUsedThisTurn == 1,
            "ambush spends all stored Energy, switches pose and retains used attack");

        state = State(); actor = state.CurrentPlayerIndex;
        attacker = Place(state, actor, 0, 1001, cards["scar_duelist"], energy: 2);
        victim = Place(state, 1 - actor, 0, 2001, Unit("rival", 2000, 600), energy: 1);
        CardInstance other = Place(state, 1 - actor, 1, 2002, Unit("other", 2000, 600), energy: 1);
        Call(state, "SetCurrentPhase", TurnPhase.Battle);
        Apply(state, GameAction.Attack(actor, attacker.InstanceId, victim.InstanceId));
        Apply(state, GameAction.EnterEndPhase(actor)); Apply(state, GameAction.EndTurn(actor));
        Call(state, "SetCurrentPhase", TurnPhase.Battle);
        Apply(state, GameAction.Attack(1 - actor, victim.InstanceId, attacker.InstanceId));
        Check(attacker.CurrentHealth == 1300, "marked rival's retaliation is reduced by 300");
        Apply(state, GameAction.Attack(1 - actor, other.InstanceId, attacker.InstanceId));
        Check(attacker.CurrentHealth == 700, "other attacker gets no mark reduction");
    }

    private static void EndEffects()
    {
        GameState state = State(cards["logistics_squire"]); int actor = state.CurrentPlayerIndex;
        CardInstance ally = Place(state, actor, 0, 1001, Unit("ally"));
        GameAction summon = GameRules.GetLegalActions(state, actor).First(a => a.Type == GameActionType.SummonMonster && a.BattlePosition == BattlePosition.Attack);
        Apply(state, summon);
        Apply(state, GameAction.ResolveTriggeredEffect(actor, summon.SourceInstanceId, ally.InstanceId));
        Check(ally.AllocatedEnergy == 1 && state.Players[actor].AvailableEnergy == 4, "Squire grants Energy without an additional available cost");
        CardInstance squire = state.Players[actor].MonsterZones.First(card => card != null && card.InstanceId == summon.SourceInstanceId);
        squire.AllocateEnergy(1); Place(state, 1 - actor, 0, 2001, Unit("enemy"));
        Call(state, "SetCurrentPhase", TurnPhase.Battle);
        Check(!GameRules.ApplyAction(state, GameAction.Attack(actor, squire.InstanceId, 2001)).Succeeded, "summoned Squire cannot attack even when charged");

        state = State(cards["defensive_outpost"]); actor = state.CurrentPlayerIndex;
        Place(state, actor, 0, 1001, cards["energy_wisp"], BattlePosition.Defense);
        Place(state, actor, 1, 1002, Unit("wall"), BattlePosition.Defense);
        Apply(state, Activation(state, state.Players[actor].Hand[0].InstanceId));
        state.Players[actor].TrySpendEnergy(4);
        Apply(state, GameAction.EnterEndPhase(actor));
        Check(state.Players[actor].AvailableEnergy == 3 && !state.HasPendingTriggeredEffect,
            "Wisp and Outpost each restore one at own End, without a maintenance prompt");
        Apply(state, GameAction.EndTurn(actor));
        Check(state.Players[actor].AvailableEnergy == 3, "EndTurn does not repeat energy effects");
    }

    private static void CopyLimits()
    {
        var deck = Enumerable.Repeat(Unit("filler"), 28).Concat(new[] { cards["energy_wisp"], cards["energy_wisp"] }).ToArray();
        GameState.CreateNewMatch(GameConfig.CreateDefault(), 1, deck, deck);
        deck[0] = cards["energy_wisp"];
        bool rejected = false;
        try { GameState.CreateNewMatch(GameConfig.CreateDefault(), 1, deck, deck); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "three copies of a limited support are rejected by core initialization");
    }

    private static void BotSmoke()
    {
        var list = cards.Values.Concat(cards.Values.Take(6)).ToArray();
        for (uint seed = 1; seed <= 2; seed++)
        {
            GameState state = GameState.CreateNewMatch(GameConfig.CreateDefault(), seed, list, list);
            IBot[] bots = { new NormalBot(), new EasyBot() };
            int actions = 0;
            while (!state.IsFinished && state.TurnNumber <= 50 && actions < 4000)
            {
                if (!state.HasOpenResponseWindow && !state.HasPendingTriggeredEffect &&
                    (state.CurrentPhase == TurnPhase.Recovery || state.CurrentPhase == TurnPhase.Standby))
                {
                    GameAction automatic = state.CurrentPhase == TurnPhase.Standby
                        ? GameAction.AdvancePhase(state.CurrentPlayerIndex)
                        : state.CurrentRecoveryStep == RecoveryStep.Draw
                            ? GameAction.ResolveDrawStep(state.CurrentPlayerIndex)
                            : GameAction.ResolveEnergyRefreshStep(state.CurrentPlayerIndex);
                    Apply(state, automatic); actions++; continue;
                }
                int actor = state.InteractionPlayerIndex;
                var legal = GameRules.GetLegalActions(state, actor);
                Check(legal.Count > 0, "expanded match always has a legal continuation: turn " + state.TurnNumber + " / " + state.CurrentPhase);
                if (bots[actor] is NormalBot normal) normal.Observe(state, actor);
                GameAction action = bots[actor].ChooseAction(BotGameStateView.Create(state, actor), legal);
                Check(action != null && legal.Contains(action), "bot chooses a supplied legal expansion action");
                Apply(state, action); actions++;
            }
            Check(actions < 4000 && (state.IsFinished || state.TurnNumber > 50), "expanded bot match advances without action loop");
        }
    }

    private static ActionResult Apply(GameState state, GameAction action)
    {
        ActionResult result = GameRules.ApplyAction(state, action);
        if (!result.Succeeded) throw new InvalidOperationException(action + ": " + result.ErrorMessage);
        return result;
    }
    private static void Call(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Strategic expansion FAIL: " + message);
        checks++;
    }
}
