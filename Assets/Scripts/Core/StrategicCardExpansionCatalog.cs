using System.Collections.Generic;

// Definitions for the explicit create-once Editor command. Existing assets are never regenerated.
public static class StrategicCardExpansionCatalog
{
    public static IReadOnlyList<CardDefinition> Create()
    {
        var cards = new List<CardDefinition>();
        cards.Add(Spell("stop_defense", "Stop Defense",
            "Pay 1 Energy; target 1 Defense Position monster your opponent controls; change it to Attack Position.",
            Active("stop_defense", EffectOperationType.ChangeSelectedMonsterToAttack, target: EffectTargetType.OpponentDefenseMonster, energy: 1)));
        cards.Add(Spell("energy_reserve", "Energy Reserve",
            "Restore 1 available Energy. If your LP are lower than your opponent's, restore 1 additional available Energy. You cannot exceed your maximum Energy.",
            Active("energy_reserve", EffectOperationType.RestoreEnergyWithLpBonus, secondary: 1,
                condition: EffectConditionType.AvailableEnergyBelowMax)));
        cards.Add(Spell("emergency_reinforcements", "Emergency Reinforcements",
            "If you controlled fewer monsters than your opponent at the start of this Main Phase: Restore 2 available Energy, up to your maximum.",
            Active("emergency_reinforcements", EffectOperationType.RestoreAvailableEnergy, 2,
                condition: EffectConditionType.FewerMonstersAtMainStart)));
        cards.Add(Spell("energy_reallocation", "Energy Reallocation",
            "Target 1 monster you control with allocated Energy; remove 1 of its Energy, then restore 1 available Energy, up to your maximum.",
            Active("energy_reallocation", EffectOperationType.ReallocateEnergyToPlayer,
                target: EffectTargetType.OwnMonsterWithEnergy, condition: EffectConditionType.AvailableEnergyBelowMax)));
        cards.Add(Spell("elite_summons", "Elite Summons",
            "Pay 1 Energy; add 1 Advanced, Supreme, or Ultimate Monster from your Deck to your hand, reveal it, then shuffle your Deck. If your LP were at least 1500 lower than your opponent's at the start of this Main Phase, pay no Energy instead.",
            Active("elite_summons", EffectOperationType.SearchSelectedCard, target: EffectTargetType.OwnDeckHighRankMonster,
                costs: new[] { new EffectCostDefinition(EffectCostType.EnergyWithLpDeficitDiscount, 1, 1500) })));
        cards.Add(Spell("battlefield_medicine", "Battlefield Medicine",
            "Target 1 wounded monster you control; restore 300 HP to it, up to its current maximum HP.",
            Active("battlefield_medicine", EffectOperationType.RestoreMonsterHealth, 300, target: EffectTargetType.OwnWoundedMonster)));
        cards.Add(Spell("change_of_plans", "Change of Plans",
            "Discard 1 other card from your hand; draw 2 cards.",
            Active("change_of_plans", EffectOperationType.DrawCards, 2, target: EffectTargetType.OwnOtherHandCard,
                costs: new[] { new EffectCostDefinition(EffectCostType.DiscardFromHand, 1) })));
        cards.Add(Spell("dispel_magic", "Dispel Magic",
            "Pay 1 Energy; target 1 face-up Equip or Field Spell your opponent controls; send it to the Graveyard.",
            Active("dispel_magic", EffectOperationType.RemoveSelectedSupport, target: EffectTargetType.OpponentFaceUpSupport, energy: 1)));
        cards.Add(Spell("recover_the_fallen", "Recover the Fallen",
            "Pay 1 Energy; target 1 Basic Monster in your Graveyard; add it to your hand.",
            Active("recover_the_fallen", EffectOperationType.RecoverSelectedCard, target: EffectTargetType.OwnGraveyardBasicMonster, energy: 1)));
        cards.Add(Spell("defensive_outpost", "Defensive Outpost",
            "During your End Phase, if you control at least 2 Defense Position monsters: Restore 1 available Energy, up to your maximum.",
            new EffectDefinition("defensive_outpost.field", EffectTriggerType.ManualActivation, EffectKind.Continuous,
                EffectPhaseMask.Main, EffectTurnScope.YourTurn, EffectActivationWindow.OpenGameState,
                EffectActivationSource.Standard, null, null, EffectTargetType.None, 0, null), SpellType.Field,
            Active("defensive_outpost.energy", EffectOperationType.RestoreAvailableEnergy,
                trigger: EffectTriggerType.EndPhase, condition: EffectConditionType.OwnDefenseMonstersAtLeast, conditionAmount: 2)));

        cards.Add(Monster("battle_herald", "Battle Herald", MonsterRank.Basic, 750, 350, 150, 1,
            "While this card is face-up on the field, Attack Position monsters you control gain 200 ATK. This effect stacks with other copies.",
            Aura("battle_herald.aura", false)));
        cards.Add(Monster("bastion_keeper", "Bastion Keeper", MonsterRank.Basic, 850, 200, 300, 1,
            "While this card is face-up on the field, Defense Position monsters you control gain 200 DEF. This effect stacks with other copies.",
            Aura("bastion_keeper.aura", true)));
        cards.Add(Monster("logistics_squire", "Logistics Squire", MonsterRank.Basic, 600, 200, 150, 1,
            "When this card is summoned: You can attach 1 Energy to 1 other monster you control, without spending available Energy. This card cannot attack during the turn it is summoned.",
            Active("logistics_squire.supply", EffectOperationType.AllocateSelectedEnergy,
                target: EffectTargetType.OwnOtherMonster, trigger: EffectTriggerType.OnSummoned),
            Passive("logistics_squire.rest", EffectOperationType.CannotAttackOnSummonTurn)));
        cards.Add(Monster("bloodfang_raider", "Bloodfang Raider", MonsterRank.Basic, 900, 650, 100, 1,
            "After this card inflicts battle damage to a monster's HP: Restore HP to this card equal to 50% of the HP actually lost, rounded down, up to 300 HP per battle. This cannot revive this card or exceed its current maximum HP.",
            Passive("bloodfang_raider.drain", EffectOperationType.Lifesteal, 50, 300)));
        cards.Add(Monster("scar_duelist", "Scar Duelist", MonsterRank.Advanced, 1600, 1000, 250, 2,
            "After this card inflicts battle damage to a monster's HP, mark that monster until the end of your opponent's next turn. Reduce the next battle damage that marked monster would inflict to this card by 300. The mark is removed after reducing damage or if either monster leaves the field.",
            Passive("scar_duelist.mark", EffectOperationType.RetaliationMark, 300)));
        cards.Add(Monster("siegebreaker", "Siegebreaker", MonsterRank.Advanced, 1700, 1050, 250, 2,
            "When this card battles a Defense Position monster, calculate damage as if that monster had 300 less DEF, to a minimum of 0. No excess damage is dealt to its controller.",
            Passive("siegebreaker.breach", EffectOperationType.PierceDefense, 300)));
        cards.Add(Monster("ambush_lancer", "Ambush Lancer", MonsterRank.Basic, 1000, 500, 400, 1,
            "After this card finishes attacking, if it is still in Attack Position: You can remove all Energy from it to change it to Defense Position. This does not restore its used attacks.",
            Active("ambush_lancer.hide", EffectOperationType.SwitchToDefenseAfterBattle, trigger: EffectTriggerType.AfterBattle)));
        cards.Add(Monster("thorn_bastion", "Thorn Bastion", MonsterRank.Advanced, 1600, 400, 600, 2,
            "If this Defense Position card loses HP from battle, inflict effect damage to the attacking monster equal to the HP this card actually lost, up to 250, even if this card is KO'd. This reflected damage does not trigger further reflection or lifesteal.",
            Passive("thorn_bastion.thorns", EffectOperationType.ReflectBattleDamage, 250)));
        cards.Add(Monster("aegis_sentinel", "Aegis Sentinel", MonsterRank.Supreme, 2500, 650, 700, 2,
            "While this card is face-up on the field, your opponent must choose it as an attack target before your other monsters, regardless of its battle position. If multiple monsters have this effect, your opponent can choose any of them. This does not restrict effect targets.",
            Passive("aegis_sentinel.priority", EffectOperationType.AttackPriority)));
        cards.Add(Monster("field_medic", "Field Medic", MonsterRank.Basic, 850, 200, 250, 1,
            "During your Main Phase: Remove 1 Energy from this card; target 1 wounded monster you control; restore 400 HP to it, up to its current maximum HP.",
            Active("field_medic.heal", EffectOperationType.RestoreMonsterHealth, 400, target: EffectTargetType.OwnWoundedMonster,
                costs: new[] { new EffectCostDefinition(EffectCostType.RemoveAllocatedEnergy, 1) })));
        cards.Add(Monster("static_leech", "Static Leech", MonsterRank.Basic, 750, 350, 200, 1,
            "During your Main Phase: Remove 1 Energy from this card; target 1 opponent's monster with allocated Energy; remove 1 of its Energy. Neither player recovers available Energy.",
            Active("static_leech.drain", EffectOperationType.RemoveSelectedEnergy, target: EffectTargetType.OpponentMonsterWithEnergy,
                costs: new[] { new EffectCostDefinition(EffectCostType.RemoveAllocatedEnergy, 1) })));
        cards.Add(Monster("veil_strategist", "Veil Strategist", MonsterRank.Advanced, 1600, 700, 450, 2,
            "When this card is summoned: You can add 1 Normal Trap from your Graveyard to your hand. That card must be Set normally and cannot be activated during the turn it is Set.",
            Active("veil_strategist.recover", EffectOperationType.RecoverSelectedCard,
                target: EffectTargetType.OwnGraveyardNormalTrap, trigger: EffectTriggerType.OnSummoned)));

        cards.Add(Monster("arcane_archivist", "Arcane Archivist", MonsterRank.Basic, 700, 200, 200, 1,
            "During your Main Phase: Pay 1 Energy; add 1 Spell or Trap from your Deck to your hand, reveal it, then shuffle your Deck. You can include up to 2 copies of this card in your Deck.",
            new[] { Active("arcane_archivist.search", EffectOperationType.SearchSelectedCard,
                target: EffectTargetType.OwnDeckSpellOrTrap, energy: 1) }, 2));
        cards.Add(Monster("energy_wisp", "Energy Wisp", MonsterRank.Basic, 750, 250, 200, 1,
            "During your End Phase: Restore 1 available Energy, up to your maximum. You can include up to 2 copies of this card in your Deck.",
            new[] { Active("energy_wisp.restore", EffectOperationType.RestoreAvailableEnergy, trigger: EffectTriggerType.EndPhase) }, 2));
        return cards;
    }

    private static EffectDefinition Active(string id, EffectOperationType operation, int amount = 1,
        EffectTargetType target = EffectTargetType.None, int energy = 0,
        EffectTriggerType trigger = EffectTriggerType.ManualActivation, EffectConditionType condition = EffectConditionType.None,
        int conditionAmount = 0, int secondary = 0, IReadOnlyList<EffectCostDefinition> costs = null)
        => new EffectDefinition(id, trigger, EffectKind.Activated,
            trigger == EffectTriggerType.EndPhase ? EffectPhaseMask.End : trigger == EffectTriggerType.AfterBattle ? EffectPhaseMask.Battle : EffectPhaseMask.Main,
            EffectTurnScope.YourTurn, EffectActivationWindow.OpenGameState, EffectActivationSource.Standard,
            condition == EffectConditionType.None ? null : new[] { new EffectConditionDefinition(condition, conditionAmount) },
            costs ?? (energy == 0 ? null : new[] { new EffectCostDefinition(EffectCostType.AvailableEnergy, energy) }),
            target, target == EffectTargetType.None ? 0 : 1, new[] { new EffectOperationDefinition(operation, amount, secondary) });

    private static EffectDefinition Passive(string id, EffectOperationType operation, int amount = 1, int secondary = 0)
        => new EffectDefinition(id, EffectTriggerType.WhileOnField, EffectKind.Continuous, EffectPhaseMask.Any,
            EffectTurnScope.EitherTurn, EffectActivationWindow.OpenGameState, EffectActivationSource.Standard,
            null, null, EffectTargetType.None, 0, new[] { new EffectOperationDefinition(operation, amount, secondary) });

    private static EffectDefinition Aura(string id, bool defense)
        => new EffectDefinition(id, EffectTriggerType.WhileOnField, EffectKind.Continuous, EffectPhaseMask.Any,
            EffectTurnScope.EitherTurn, EffectActivationWindow.OpenGameState, EffectActivationSource.Standard,
            null, null, EffectTargetType.None, 0, null, continuousModifiers: new[] { new PositionAuraModifier(defense, 200) });

    private static CardDefinition Spell(string id, string name, string text, EffectDefinition effect,
        SpellType type = SpellType.Normal, EffectDefinition second = null)
        => new CardDefinition(id, name, CardCategory.Spell, text, MonsterAttribute.Wind, MonsterRank.Basic,
            MonsterType.Spellcaster, 0, 0, 0, 0, type, TrapType.Normal, 0, CardSpeed.Speed1,
            second == null ? new[] { effect } : new[] { effect, second });

    private static CardDefinition Monster(string id, string name, MonsterRank rank, int hp, int attack, int defense,
        int energy, string text, params EffectDefinition[] effects)
        => Monster(id, name, rank, hp, attack, defense, energy, text, effects, 0);

    private static CardDefinition Monster(string id, string name, MonsterRank rank, int hp, int attack, int defense,
        int energy, string text, IReadOnlyList<EffectDefinition> effects, int limit)
        => new CardDefinition(id, name, CardCategory.Monster, text, Attribute(id), rank, Type(id),
            hp, attack, defense, energy, SpellType.Normal, TrapType.Normal, (int)rank, CardSpeed.Speed1, effects, limit);

    private static MonsterAttribute Attribute(string id)
    {
        switch (id)
        {
            case "battle_herald": case "bloodfang_raider": return MonsterAttribute.Fire;
            case "bastion_keeper": case "siegebreaker": return MonsterAttribute.Earth;
            case "field_medic": case "energy_wisp": return MonsterAttribute.Water;
            case "logistics_squire": case "scar_duelist": case "aegis_sentinel": case "static_leech": return MonsterAttribute.Metal;
            case "ambush_lancer": case "veil_strategist": case "arcane_archivist": return MonsterAttribute.Wind;
            default: return MonsterAttribute.Wood;
        }
    }

    private static MonsterType Type(string id)
    {
        switch (id)
        {
            case "bastion_keeper": case "siegebreaker": case "aegis_sentinel": return MonsterType.Machine;
            case "bloodfang_raider": return MonsterType.Fiend;
            case "thorn_bastion": return MonsterType.Plant;
            case "field_medic": return MonsterType.Fairy;
            case "static_leech": return MonsterType.Insect;
            case "veil_strategist": case "arcane_archivist": return MonsterType.Spellcaster;
            case "energy_wisp": return MonsterType.Spirit;
            default: return MonsterType.Warrior;
        }
    }
}
