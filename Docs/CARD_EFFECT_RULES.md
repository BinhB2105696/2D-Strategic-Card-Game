# CARD EFFECT RULES

This document defines the first safe boundary for reusable card effects. It is
not a final list of every possible effect and does not yet define the chain UI.

## 1. Activation flow

Every activated effect follows this conceptual flow:

```text
Activation timing
    -> Condition
    -> Cost availability
    -> Target selection
    -> Final validation
    -> Pay cost
    -> Resolve effect
    -> Emit GameEvents
```

- **Activation timing** answers when the effect is allowed to be requested.
- **Condition** is a requirement and is not consumed, such as “during Standby”
  or “while the opponent controls a Monster”.
- **Cost** must be payable before activation. Once the effect is accepted, the
  cost is paid even if another effect later negates the effect.
- **Target** identifies the exact card/player affected. Physical cards use
  `instanceId`, not card names or Unity objects.
- **Effect** changes `GameState` only after all requirements are valid.
- **GameEvent** describes the accepted result for UI animation, replay logs,
  sound, and network presentation.

The UI may ask the player to choose targets after showing/confirming the cost.
However, the core receives a complete request and validates condition, cost,
and targets before mutating state. This prevents an invalid replay or network
message from charging a cost and leaving a half-resolved action.

## 2. Core safety rules

1. Effects never access Canvas, GameObject, Sprite, animation, or input APIs.
2. Effects receive IDs and primitive values through deterministic actions.
3. All random choices use the match `SeededGameRandom`.
4. A failed validation changes no gameplay state and pays no cost.
5. A successfully paid cost is not refunded merely because the effect is
   negated later.
6. Main Phase and End Phase cannot be skipped.
7. Recovery Phase itself cannot be skipped. Draw Step and Energy Refresh Step
   may be skipped independently.
8. Standby Phase and Battle Phase may be skipped by explicit effects.
9. Effects that alter attack count modify `MaxAttacksThisTurn`; they do not
   erase `AttacksUsedThisTurn` unless a card explicitly says so.
10. Each supported operation should be reusable by more than one card. Avoid
    writing a unique rules branch identified by a card name.

## 3. Current C# contracts

- `CardEffectData`: one effect entry embedded in `CardData`. It stores kind,
  trigger/timing, component slots, and optional usage limit.
- `EffectConditionAsset`, `EffectCostAsset`, `EffectTargetAsset`,
  `EffectResolutionAsset`, and `ContinuousModifierAsset`: reusable Unity assets
  assigned to the matching slots. Current reusable assets include Pay Energy,
  Draw Cards, and Skip Next Opponent Draw.
- `EffectDefinition`: immutable pure-C# copy used by the running match. It has
  no Sprite, ScriptableObject, GameObject, or other presentation dependency.
- `EffectContext`: the match, activating player, source instance, and selected
  target instance IDs.
- `IEffectCondition`: checks a requirement without changing state.
- `IEffectCost`: first checks affordability, then pays the cost.
- `IEffectTargetRule`: validates the selected targets without changing state.
- `ICardEffect`: resolves the gameplay change and appends `GameEvent` results.
- `EffectResolver`: performs the shared validation and resolution order.

`CardData` owns an ordered list of effect entries. Each entry references
reusable components; it does not reference a whole-effect asset. Components
are organized under `Assets/Data/EffectComponents` by role.

The visible Effect Text remains authored for the player and should mention
costs clearly. Rules never parse that prose. Machine-readable costs and
operations live in the component slots and are validated separately.

`Max Uses = 0` means unlimited. Otherwise Count By selects per-instance or
per-card-ID counting, and Reset selects per-turn or per-match counting.

The executable slice now includes Manual Activation, targeted and ordered
multi-target effects, Available Energy costs, persistent/temporary stat
modifiers, Equip and Field lifetime, Set Normal/Counter Traps, attack and
activation response windows, Speed 1/2/3 LIFO chains, and selected OnSummoned,
OnDestroyed, and OnBattleDestroyedMonster triggers. Optional search/recovery
triggers expose a pending choice and use the seeded match RNG when the Deck is
shuffled. Unsupported activation sources or effect combinations must still be
rejected clearly rather than treated as playable.

## 4. First implementation slice

The recommended first playable slice is:

1. Activate a Normal Spell from hand during Main Phase.
2. Require and select an empty shared Spell/Trap Zone.
3. Move the Spell from Hand to that zone face-up.
4. Convert its embedded effect and referenced components into an
   `EffectDefinition`.
5. Pay its Available Energy cost.
6. Resolve the reusable Draw Cards operation.
7. Move the resolved Spell from its zone to Graveyard face-up.
8. Emit events for placement, activation, cost, resolution, and movement.

Future variants use explicit alternate flows rather than bypassing this rule
silently. Examples include hand-activated effects that do not need a zone,
Equip Spells that remain in a shared Spell/Trap Zone, and Field Spells that use
a dedicated Field Zone.

Trap setting, response windows, Speed 2/3 chains, Equip duration, and Field
Spell ownership described above are now connected through the same validated
action/effect pipeline. Future components must continue using that boundary.
