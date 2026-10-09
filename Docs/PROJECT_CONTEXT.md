# PROJECT CONTEXT

> Last updated: 9 October 2026
>
> Status tags:
> - **[CONFIRMED]**: implement as written unless the user changes it.
> - **[TENTATIVE]**: current working value; keep it configurable.
> - **[OPEN]**: not decided; use the documented default, mark it `TODO(OPEN)`, and continue.

## 1. Purpose

This is the current working context for the 2D Strategic Card Game graduation thesis project.

- `README.md` is the public overview.
- This file contains detailed gameplay, scope, architecture, progress, and AI-development rules.
- Newer confirmed decisions in this file take priority over older discussions and README text.

## 2. Official Thesis Scope

Registered title:

**Development of a 2D Strategic Card Game with an AI Bot System and PvE/PvP Modes**

The registered description promises:

- A 2D strategic card game made with Unity.
- Multiple card types, attributes, effects, and tactical strategies.
- PvE with multi-tiered bots using rule-based and decision-making algorithms.
- PvP between two human players.
- A scalable core that can be extended with more cards, mechanics, and strategies.

The registration does **not** require exactly three bot difficulties. Current priorities are:

1. Stable complete game and rules.
2. Easy Bot and Normal Bot.
3. Online PvP and Android/Windows builds.
4. Headless bot-vs-bot simulation and thesis metrics.
5. Hard Bot using advanced search if time permits.
6. Machine learning/self-play only if the core product is already stable.

Deck Building is postponed until the project has enough cards to justify it.

## 3. AI Assistant Rules

### Allowed without asking

1. Read the project and relevant files before changing code.
2. Create and edit C# scripts under `Assets/Scripts/` when required by the agreed current step.
3. Refactor scripts when justified, while preserving working behavior and summarizing the change.
4. Create or update files under `Docs/` and make small progress/roadmap edits to `README.md`.
5. Create Editor scripts that generate Unity assets through Unity APIs instead of hand-writing YAML.

### Ask or warn first

1. Installing packages, plugins, SDKs, networking frameworks, or external dependencies.
2. Changing Unity version, render pipeline, package versions, build settings, or project-wide settings.
3. Deleting, renaming, or moving files. Never delete `.meta` files or create them by hand.
4. Hand-editing `.unity`, `.prefab`, or `.asset` YAML. Prefer exact Unity Editor instructions or an Editor tool.
5. Rewriting Git history, force-pushing, destructive resets, or deleting branches.
6. Changing any **[CONFIRMED]** gameplay rule.

### Never

1. Treat files in `Library/`, `Temp/`, `Logs/`, `obj/`, or build-output folders as source files.
2. Put API keys, passwords, tokens, or secrets in source code.
3. Silently choose between conflicting gameplay rules.

### Teaching and Unity UI instructions

The user is learning C# and Unity. When introducing a new concept, explain its purpose in simple Vietnamese.

Every manual Unity or Visual Studio instruction must state:

1. **Application and window/panel**: for example `Unity Editor > Project window` or `Visual Studio 2022 > Solution Explorer`.
2. **Current editing context**: Scene Mode, Prefab Mode, Play Mode, or Edit Mode.
3. **Object/file to select** before looking for a control.
4. **Exact location and visual description** of the control, such as "the triangle Play icon in the center of Unity's top toolbar".
5. **Exact menu path and useful shortcut** when available, such as `Edit > Play` or `Ctrl+P`.
6. **Expected visible result** so the user can verify the action.
7. A warning when the action must not be performed in Play Mode because changes could be lost.

Do not write vague instructions such as "press Play" or "assign the object" without identifying where and how.

After each development step, report:

- Files created or changed.
- Exact Unity test steps.
- Expected results.
- Common problems and how to recognize them.
- A suggested Git commit checkpoint.

## 4. Constraints

- Unity 2022.3.62f3 LTS and Visual Studio 2022.
- C# is the primary language.
- Development machine: Ryzen 5 5500U, 8 GB RAM, integrated Radeon Graphics, Windows 10 LTSC.
- Target platforms in priority order: **Android first, Windows second**.
- Mobile-first UI: portrait, large touch targets, no gameplay interaction that requires hover or right-click.
- Thesis defense is expected in early December 2026.

## 5. Scope Priority

### Tier 1: must be stable

- Complete playable card game with correct core rules and functional UI.
- Easy rule-based bot.
- Normal heuristic bot.
- Online 1v1 PvP with player identity/account support.
- Android build, then Windows build.
- Headless bot-vs-bot simulation and metrics for thesis evaluation.

### Tier 2: build if Tier 1 is stable

- Hard Bot using Minimax/Alpha-Beta or another suitable advanced algorithm.
- More cards and decks, balancing, visual polish, and audio.

### Tier 3: only if time remains

- Deck Building.
- Machine learning/self-play experiments.

Higher-tier work must not destabilize a lower tier.

## 6. Match Fundamentals

- Player starting HP: 4000 **[TENTATIVE]**.
- Deck size: 30-40 cards **[TENTATIVE]**.
- Starting hand: 4 cards **[TENTATIVE]**.
- Normal draw: 1 card each turn **[TENTATIVE]**.
- Monster Zones: 3 **[CONFIRMED]**.
- Spell/Trap Zones: 3 **[CONFIRMED]**.
- Primary win condition: reduce the opponent's HP to 0 **[CONFIRMED]**.
- Drawing from an empty deck draws nothing; deck-out is not an automatic loss **[CONFIRMED]**.
- Target match duration: 10-15 minutes **[TENTATIVE]**.
- First player: random; the first player draws normally **[OPEN, default]**.

All configurable numbers must live in one `GameConfig`, not as scattered magic numbers.

## 7. Energy and Summoning

- Base maximum Energy: 5 **[CONFIRMED]**.
- After drawing, Available Energy resets to the player's current Max Energy **[CONFIRMED]**.
- Unused Available Energy does not carry to the next turn.
- Effects may increase Max Energy or add/restore Available Energy.
- Normal Summon is unlimited per turn, costs 1 Energy, and is limited by zones and available Energy **[CONFIRMED]**.
- Tribute Summon costs 1 Energy plus tributes **[CONFIRMED, balance may change]**:
  - Basic: 0.
  - Advanced: 1.
  - Supreme: 2.
  - Ultimate: 3.
- Energy and damage on tribute monsters are not inherited.
- Energy may be allocated to a monster during Main Phase.
- Allocated Energy stays after attacking and is not refunded to the player **[CONFIRMED]**.
- Card effects may remove allocated Energy.
- Allocated Energy does not increase ATK/DEF **[OPEN, default]**.
- Energy per monster has no cap **[OPEN, default]**.
- When a monster is destroyed, its owner loses 100 HP per allocated Energy **[OPEN, default]**.

`PlayerState` must distinguish `MaxEnergy` from `AvailableEnergy`.

## 8. Turn and Combat Rules

### Recovery Phase

Recovery Phase contains two independently resolved steps:

1. Draw Step: normally draw one card.
2. Energy Refresh Step: normally reset Available Energy to Max Energy.

Effects may skip the next Draw Step or Energy Refresh Step independently, but
ordinary card effects must not skip the entire Recovery Phase.
Skip consequences are attached to a specific turn, not stored as a stack of
future skipped turns. Repeating the same skip for one turn does not extend it.

### Standby Phase

Resolve Standby triggers and maintenance costs. It currently passes without
choices when no effect is pending. Effects may skip a future Standby Phase.

### Main Phase

- Normal Summon and Tribute Summon.
- Allocate Energy to monsters.
- Activate or set allowed cards.
- A monster already on the field at the start of the turn may change position once.
- A monster summoned this turn cannot normally change position.

### Battle Phase

- Only Attack Position monsters can attack.
- A monster normally attacks once per turn.
- Its allocated Energy must meet its attack requirement.
- A newly summoned monster may attack if eligible **[CONFIRMED]**.
- The first turn of the match has no Battle Phase; it proceeds from Main Phase
  directly to End Phase **[CONFIRMED]**.
- Main Phase and End Phase cannot be skipped. Effects may skip a future Battle
  Phase without altering the rest of the turn structure **[CONFIRMED]**.
- Default interpretation: only the first player's first turn is the first turn **[OPEN]**.
- Against Attack Position: defender loses HP equal to attacker ATK; attacker takes no counter-damage **[OPEN, default]**.
- Against Defense Position: damage to monster HP is `max(0, ATK - DEF)`.
- Overflow damage never reaches player HP **[CONFIRMED]**.
- Monster damage persists across turns **[OPEN, default]**.
- A monster at 0 HP goes to the Graveyard and triggers the destruction penalty.
- Direct attack is allowed only when the opponent has no monsters; damage equals attacker ATK **[OPEN, default]**.

### End Phase

Resolve end-of-turn effects and pass control to the opponent.
From Main or Battle, `EnterEndPhase` enters End without first entering any
intermediate phase. Going directly from Main records Battle as skipped and
resolves any Battle-skip consequence assigned to that turn, including turn 1.
It never carries an unused skip into a later turn. `EndTurn` remains a
separate action available only in End.

## 9. Cards and Decks

- Categories: Monster, Spell, Trap.
- Monster attributes: Metal, Wood, Fire, Water, Earth, Wind.
- Monster ranks: Basic, Advanced, Supreme, Ultimate.
- Monster types: Dragon, Warrior, Spellcaster, Fairy, Machine, Fiend,
  Animal, Insect, Plant, Spirit, Elf, Undead.
- Spell types: Normal, Equip, Field.
- Trap types: Normal, Counter.
- Spell is Speed 1; Normal Trap is Speed 2; Counter Trap is Speed 3.
- Activation Speed must be derived from card category/type, not entered inconsistently by hand.
- Start with a simple last-in-first-out chain and avoid premature generalization.
- Activation costs are structured as component slots in each CardData effect.
- A standard Spell/Trap activated from Hand requires an appropriate empty zone
  and first moves onto the field. Cost and effect then resolve from the field.
- A resolved Normal Spell moves from its shared Spell/Trap Zone to Graveyard
  face-down, releasing that zone **[CONFIRMED]**.
- Equip Spells will target a Monster and remain in a shared Spell/Trap Zone
  while equipped. Field Spells will remain in a dedicated Field Zone.
- Future hand-activated Spell/Trap effects may explicitly bypass field
  placement; this is an alternate effect rule, not the default flow.

Initial deck direction:

- Shared common cards usable by multiple decks.
- Three sample archetypes: Aggro, Sustain/Defense, and Control.
- Heavy overlap is allowed; 30-card decks do not require 30 unique designs each.
- Combo is optional after the first three archetypes work.

## 10. Card UI

- Canvas UI with `Image` and TextMeshPro, not SpriteRenderer.
- Layout: Name, Attribute icon, Card Type, Artwork, HP/ATK/DEF for monsters, and Effect text.
- Monster Rank has its own display area. Monster Card Type displays the
  Monster Type in brackets, for example `[Dragon]`.
- Spell/Trap Card Type text uses brackets and text only, for example
  `[Normal Spell]`, `[Field Spell]`, and `[Counter Trap]`.
- Monster Rank is displayed in angle brackets, for example `<Advanced>`.
- Face-down cards show only the shared Card Back. All front-face text, icons,
  artwork, rank, and stats must be disabled while face-down.
- Spell and Trap use shared category icons.
- `Card_Base` contains common UI.
- `Card_Monster` is a variant adding HP/ATK/DEF.
- `Card_SpellTrap` is a variant without monster stats.
- Current visuals are functional prototypes, not final art.
- Mobile gameplay will include a card detail panel opened by tapping/selecting
  a card so players can read its full attributes and effect text clearly.
- Default orientation: portrait, Canvas Scaler reference 1080x1920, Match 0
  (Width) **[CONFIRMED]**.

### Board UI direction

- Use one background image; gameplay zones remain separate Unity UI objects.
- Each player has 3 Monster Zones, 3 Spell/Trap Zones, a Deck area, a
  Graveyard area, and a Hand area.
- Monster and Spell/Trap Zones use the same compact portrait rectangle. A
  Defense Position Monster may rotate and extend beyond the visual outline;
  zones must not use Mask or RectMask2D.
- Empty zones use a thin white translucent outline with only slight corner
  rounding. The visual border is independent from its transparent raycast hit
  area. A small 9-sliced outline Sprite, four thin child Images, or a later
  procedural UI Graphic are valid implementations.
- Show the turn number and current phase near the upper-right HUD, beside the
  in-match settings button.
- Use a compact phase-control button on the right side. During Main Phase it
  expands to Battle and End choices; during Battle Phase only End is needed.
- Recovery and an empty Standby advance automatically after their required
  steps/triggers resolve, so they do not need manual transition buttons.
- Entering Battle or ending the turn uses a confirmation overlay. An
  unavailable Battle choice is disabled and may show a short reason.
- Board UI sends GameActions and renders GameEvents; it never changes rules or
  GameState directly.

## 11. Architecture Boundaries

```text
Unity authoring data        CardData (ScriptableObject, Sprite references)
        ↓ conversion/catalog lookup
Pure game definitions      CardDefinition (IDs, enums, numbers, effect definitions)
Pure runtime core          GameConfig, CardInstance, PlayerState, GameState,
                           GameRules, TurnManager, CombatSystem, GameAction
Consumers                  Human input, Bots, Simulator, Replay, Network
Unity presentation         CardView, BoardView, HandView, HUD, animation
```

Rules:

1. Core gameplay must not depend on Canvas, UI objects, `MonoBehaviour`, animation, or input APIs.
2. `CardData` is Unity authoring data. The core receives a pure `CardDefinition` without `Sprite` or UI references.
3. Each card definition has a stable unique `cardId` for deck/save/network references.
4. Each physical copy in a match has a separate `instanceId` so duplicate cards can be targeted unambiguously.
5. `CardInstance` stores current stats, allocated Energy, buffs/debuffs, position, attack/position flags, and zone state.
6. Every player move is a serializable `GameAction` containing IDs and primitive values, never GameObjects or Sprites.
7. The core exposes legal actions and applies validated actions. Humans, bots, replay, and network all use the same path.
8. Randomness comes from one seeded game RNG. Avoid `UnityEngine.Random` inside core rules.
9. Given the same initial state, seed, and ordered actions, the result must be deterministic.
10. Save/replay formats must include a schema version. Do not use asset paths as permanent IDs.
11. Start effects with a small set of concrete types and generalize only after repeated patterns appear.

This preparation supports headless simulation, Android/Windows, replay, save/load, and PvP without implementing all of them immediately.

## 12. Bots and Evaluation

- `IBot` receives a safe game-state view and chooses a legal `GameAction`.
- Easy Bot: fixed rules.
- Normal Bot: heuristic scoring and short-term action evaluation.
- Hard Bot: limited-depth search such as Minimax/Alpha-Beta **[OPTIONAL AFTER TIER 1]**.
- ML/self-play: optional experiment only.

The headless simulator must run matches without UI and record at least:

- Winner and matchup.
- Random seed and who went first.
- Turn count.
- Energy spent, allocated, and unused.
- Decision time per move.
- Invalid-action or timeout count.

CSV output will support thesis tables and charts.

## 13. PvP and Networking

"Networking solution" means the technology and service used to identify players, create/join matches, carry messages between devices, validate actions, handle disconnects, and optionally save player data.

Current recommendation **[TENTATIVE; requires a feasibility spike and approval before packages are installed]**:

- Unity Authentication for player identity. Start with anonymous accounts for testing; add Username & Password or Unity Player Accounts if cross-device recovery is required.
- Unity Multiplayer Services SDK Sessions for Lobby/Relay room creation and join codes.
- Unity Relay to connect Android and Windows players without exposing their IP addresses or operating a dedicated server.
- Netcode for GameObjects as the first transport adapter, sending compact `GameAction` messages rather than synchronizing the entire UI/core as NetworkObjects.
- Unity Cloud Save only if persistent profiles/decks are needed; it is not required for the first online match.

Initial model:

- Host-authoritative: the host owns the canonical `GameState`, validates actions with the shared core, and broadcasts accepted actions/state checks.
- Relay is a message intermediary, not a dedicated authoritative game server.
- If the host disconnects, the first version may end the match. Host migration is optional.
- Dedicated server authority is an advanced upgrade only if schedule and service limits permit.

Do not install networking packages until local core actions are deterministic and a short spike confirms Unity 2022.3 compatibility, Android/Windows connection, account flow, free-tier limits, and acceptable complexity.

## 14. Open Decisions and Defaults

| Question | Default |
|---|---|
| HP penalty per Energy on destroyed monster | 100 |
| Allocated Energy boosts ATK/DEF | No |
| Max allocated Energy per monster | No cap |
| Monster damage persists | Yes |
| Counter-damage against Attack Position | None |
| Direct attack condition | Opponent has no monsters |
| First-turn attack restriction | First player's first turn only |
| First player and draw | Random; first player draws |
| Mobile layout | Portrait, 1080x1920 reference, Match 0 (Width) |
| Networking stack | Tentative UGS stack in section 13; decide after spike |

## 15. Current Progress

Completed or explored:

- GitHub repository and Unity 2022.3.62f3 project.
- Visual Studio 2022 and Android build tooling.
- Basic Unity concepts, Canvas UI, Image, TextMeshPro, Prefabs, and Variants.
- Card UI prototype and `Card_Base` / `Card_Monster` / `Card_SpellTrap` direction.
- `CardData.cs` ScriptableObject with initial enums and static fields.
- `CardData.cs` category-specific Inspector, stable ID field, Monster Type,
  derived Speed/tribute values, and bounded base stats.
- `CardView.cs` displaying category-specific text, artwork, effects, rank,
  attribute icon, monster base stats, category frame, and face-down Card Back.
- Shared `CardVisualLibrary` for frames, attribute icons, and Card Back.
- Fire Dragon and Field Spell test CardData assets display in Play Mode.
- Initial card frames, six Monster attribute icons, Card Back, and serif TMP
  font assets have been integrated.
- Category-specific sample generation and CardData validation tools create and
  verify a representative set of Monster, Spell, and Trap assets.
- `CardDefinition` provides a pure runtime definition without Sprite, UI, or
  GameObject references; `CardDataConverter` creates it from Unity CardData.
- `CardInstance` assigns a deterministic per-match instance ID and stores
  mutable HP/ATK/DEF, allocated Energy, zone, face state, battle position, and
  per-turn flags independently from the shared card definition.
- `GameConfig` centralizes current configurable match values. `PlayerState` and
  `GameState` initialize two 30-40 card decks, deterministic instances, seeded
  shuffling, a random first player, four-card starting hands, HP, Energy, and
  3+3 board zones without requiring UI.
- `SeededGameRandom` version 1 supplies deterministic, bias-resistant random
  values for shuffling and future gameplay randomness.
- The basic turn flow now supports Recovery (Draw and Energy Refresh steps),
  Standby, Main, Battle, and End phases.
  `GameSession` routes actions through `GameRules`, rejects out-of-turn or
  phase-invalid actions, applies valid state changes, emits presentation-ready
  `GameEvent` values, and records accepted actions in an in-memory `MatchLog`.
- Editor smoke tests verify card instances, deterministic match initialization,
  equal-seed opening hands, the first complete phase cycle, and rejection of an
  out-of-turn action. All tests passed in Unity on 1 October 2026.
- Main Phase and combat smoke tests verify Energy allocation, summons and
  tributes, position changes, first-turn Battle skipping, attacks, destruction,
  Graveyard movement, direct damage, destruction penalties, and winner checks.
- Reusable effect contracts and the first Normal Spell flow are implemented and
  smoke-tested: activation from Hand requires an open Spell/Trap Zone, pays its
  Energy cost, resolves Draw Cards, and sends the used card to Graveyard.
- A reusable Skip Next Opponent Draw resolution component is available for
  future card effects. The core computes the affected player's next unresolved
  Draw Step regardless of whose turn the effect resolves on. Turn-specific
  skips do not stack or carry over. On 7 October, the student confirmed its
  full-turn Unity smoke test, including actual skipped draws and a later normal
  draw.
- A shared Spell/Trap activation path now supports Normal and Equip Spells,
  each player's independent Field Zone, Trap setting and next-turn activation,
  targeted effects, persistent modifiers, Field maintenance, and card cleanup
  when a persistent source or equipped target leaves play.
- A simple Speed 1/2/3 last-in-first-out chain is connected to attack and card
  activation response windows. The core enumerates legal responses and skips
  players with none, so UI prompts only appear when an actual response can be
  submitted. Counter Trap negation marks the preceding activation as negated
  without introducing direct Monster destruction.
- The fixed playable demo deck contains 30 cards: ten current designs at three
  copies each. Validation rejects missing cards, empty effect definitions,
  invalid deck size, or more than three copies. An Edit Mode bootstrap repairs
  the sample setup before Play instead of requiring a remembered manual menu
  command.
- The portrait board uses a 1080x1920 Canvas, SafeAreaRoot, separate
  Board/HUD/Overlay layers, four 3-slot rows, and zones with independent
  Border, HitArea, CardAnchor, and Highlight children. Deck, Graveyard, Field,
  Hand, HUD, turn/phase, LP, Energy, and live card state are presented from the
  active player's perspective. Opponent Monster and Spell/Trap Zone indices are
  mirrored for the rotated perspective without changing logical zone indices.
- The local hot-seat UI now submits validated GameActions for phase changes,
  basic and tribute summons, Energy allocation, position changes, attacks, and
  the existing Normal Spell flow. Tribute selection, adaptive hand layout,
  Monster Zone HP/ATK/DEF/Energy status, modal exclusivity, settings layering,
  and match-result presentation are implemented.
- By 9 October, the student had verified Play-mode interaction for automatic
  Recovery/Standby, first-turn Battle disablement, phase confirmation and turn
  changes, summons and selected tributes, Energy allocation, position changes,
  Normal Spell draw/cost/Graveyard behavior, combat damage/destruction, LP-zero
  stopping the match, perspective mirroring, and modal/layer ordering.
- On 10 October, the student entered Play Mode with the fixed deck and reported
  the current match mechanics and action sequence working as a checkpoint.
  This is an integration acceptance pass, not yet an exhaustive matrix for
  every Spell/Trap target and multi-link chain combination.
- On 7 October, the student confirmed the Direct End Phase, Recovery Skip,
  Effect Pipeline, Skip Opponent Draw, and Normal Spell smoke tests in Unity.

Known next gaps:

1. Run a focused Play-mode regression for the new all-category slice: Equip
   targets and cleanup, Field replacement/maintenance, Trap set-turn lockout,
   attack responses, Counter Trap negation, two-or-more-link LIFO resolution,
   insufficient Energy, full zones, and the match-result overlay.
2. Promote the existing response-action enumeration into a shared complete
   legal-action view for human UI, Easy/Normal bots, and headless simulation.
   Add deterministic state hashes/replay loading before networking depends on
   the action stream.
3. Perform the short networking feasibility spike described in section 13
   while the deterministic action boundary is still small. Do not install or
   commit a networking stack until the result and tradeoffs are discussed.
4. Queue accepted GameEvents for readable visual sequencing and essential
   feedback. Optional visual polish remains below bots, simulation, online PvP,
   and Android/Windows build stability.

## 16. Roadmap

Verified checkpoints:

- Step 1 card authoring/presentation foundation: **complete**.
- Step 2 pure definitions, runtime instances, configuration, seeded match
  initialization, players, deck shuffle/draw/hand, and basic state: **complete**.
- Step 3 turn/action foundation: **complete and smoke-tested**; phase flow,
  action logging, Energy allocation, summon, tribute, and position actions work.
- Step 4 combat/local-play foundation: **complete for the Monster + Normal
  Spell slice and verified in Play Mode**. A local hot-seat match can proceed
  through phase controls, card actions, combat, perspective changes, and LP-zero
  winner detection. A brief repeatable full-match regression remains useful,
  while the remaining card categories belong to Step 5.
- Playable board UI: the Unity sample scene displays and interacts with a seeded
  match's hands, zone cards, LP, Energy, deck/graveyard counts, turn, and phase.
  Opponent hands remain face-down; opponent zones mirror horizontally as the
  active-player perspective changes. Settings, Phase, and card-action modals
  have explicit runtime sorting and mutually exclusive open states.
- Step 5 card-flow foundation: **implemented and accepted as an initial Play
  Mode checkpoint**. CardData effects convert to pure EffectDefinition data;
  Normal/Equip/Field Spells, Normal/Counter Traps, targeting, persistent
  lifetime, filtered response windows, and simple LIFO chain resolution are
  connected to the local UI. Focused edge-case regression and the networking
  feasibility decision still remain before Step 5 is complete.

| Step | Target | Content | Verification |
|---|---|---|---|
| 1 | ~6 Oct | CardData/CardView gaps, IDs, icon, derived values, 5-8 test cards, functional card prefabs | Multiple card categories display correctly in Play Mode |
| 2 | ~13 Oct | Pure CardDefinition/core boundary, GameConfig, CardInstance, PlayerState, GameState, DeckData, shuffle/draw/hand | Two players initialize without UI and state prints correctly |
| 3 | ~20 Oct | Turn phases, Energy, serializable GameActions, legal-action/apply path, summon/allocation/position | Scripted turn sequence produces correct deterministic state |
| 4 | ~27 Oct | Combat, Graveyard, destruction penalty, win check, board UI, hot-seat | Full local match reaches a winner |
| 5 | ~3 Nov | Spell/Trap/effects/simple chain, initial decks, short networking feasibility spike | Complete local match with all categories; networking choice confirmed |
| 6 | ~10 Nov | Easy/Normal bots, headless simulator, CSV metrics | Human-vs-bot and bot-vs-bot batches work |
| 7 | ~17 Nov | Online host-authoritative PvP and account flow using approved solution | Android/Windows devices complete an online match |
| 8 | ~24 Nov | Android build first, Windows build, bug fixing/balancing; Hard Bot if Tier 1 is stable | Stable builds and thesis experiment data |
| 9 | 25 Nov-early Dec | Feature freeze, final experiments, report, defense preparation | Final build and thesis materials |

If schedule slips, reduce extra cards, polish, Hard Bot, and ML before compromising Tier 1 stability.
As of 10 October, the core Step 4 local-play slice is working ahead of its dated
target and the executable card-flow portion of Step 5 has an initial Play Mode
acceptance pass. The immediate critical path is a focused all-category/chain
regression, followed by the shared complete legal-action view needed by bots,
simulation, replay/state-hash checks, and networking. Do not substitute more
isolated card scripts or smoke tests for those integration milestones. Tests
remain verification for each finished slice.

## 17. Change Management

1. Discuss changes to **[CONFIRMED]** rules before implementation.
2. When a rule or default changes, update the relevant section and section 14.
3. Update README only for high-level changes.
4. If code and this document disagree, report the inconsistency instead of silently choosing one.
5. Update progress after each verified step, not merely after code is written.
