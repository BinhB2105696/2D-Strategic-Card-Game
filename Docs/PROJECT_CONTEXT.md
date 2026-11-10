# PROJECT CONTEXT

> Last updated: 9 November 2026
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

1. Stable complete game, rules, and a readable event-driven duel presentation.
2. Easy Bot and Normal Bot, plus deterministic replay/simulation support.
3. A small product shell: local/guest profile, Main Menu, PvE/PvP entry points,
   and a Deck Builder MVP.
4. Online PvP and Android/Windows builds.
5. Headless bot-vs-bot simulation and thesis metrics.
6. Hard Bot using advanced search if time permits.
7. Machine learning/self-play only if the core product is already stable.

The project now has enough card designs to justify a limited Deck Builder MVP.
It is a product-completeness requirement, not permission to build a collection,
crafting, gacha, or elaborate deck-management system before the duel, bots,
networking, and builds are stable.

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

### Tier 1A: thesis-critical and must be stable

- Complete playable card game with correct core rules and functional UI.
- Event-driven duel presentation with enough animation and feedback that a
  viewer can follow draw, summon/set/activate, attack, damage, destruction,
  Energy, phase/turn, and chain resolution without reading the Console.
- Easy rule-based bot.
- Normal heuristic bot.
- Online 1v1 PvP with player identity/account support.
- Android build, then Windows build.
- Deterministic replay/state-hash support, headless bot-vs-bot simulation, and
  metrics for thesis evaluation.

### Tier 1B: product-completeness MVP

- Local/guest profile and a simple entry/login screen; do not build a custom
  password backend.
- Main Menu routes to Deck Building, PvE, and PvP.
- Deck Builder MVP: browse cards, inspect card details, add/remove cards,
  enforce deck size and copy limits, save/select a small number of local decks,
  and provide at least two usable preset decks.
- Tier 1B work may proceed in a separate UI slice, but it must yield to Tier 1A
  correctness, online feasibility, and platform-build risks.

### Tier 2: build if Tier 1 is stable

- Hard Bot using Minimax/Alpha-Beta or another suitable advanced algorithm.
- More cards and decks, balancing, richer VFX, cosmetic animation, and audio
  beyond the functional feedback required by Tier 1A.

### Tier 3: only if time remains

- Machine learning/self-play experiments.
- Expanded account/profile, collection, filtering, crafting, reward, or gacha
  systems beyond the Deck Builder MVP.

Higher-tier work must not destabilize a lower tier.

## 6. Match Fundamentals

- Player starting LP: 6000 **[TENTATIVE, trial approved 3 November]**.
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
- When a field Monster reaches 0 HP (KO), its owner loses LP by rank: Basic
  500, Advanced 1000, Supreme 1500, Ultimate 2000. This replaces, rather than
  adds to, the old allocated-Energy penalty **[CONFIRMED, 3 November]**.
- The rank penalty applies to battle, effect damage and maximum-HP loss alike.
  Tribute or other non-KO movement does not incur it. Both players reaching
  0 LP in the same settled KO batch is a draw **[CONFIRMED, 3 November]**.

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
- A Monster at 0 HP is knocked out, goes to the Graveyard and incurs its rank
  penalty exactly once. Battle-only effects remain battle-only; effect/HP-buff
  loss KOs do not activate Gravebound Imp or Legendary Fighter's battle trigger.
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
  face-up, releasing that zone **[CONFIRMED, changed by student 21 October]**.
- Cards enter each owner's Graveyard face-up in arrival order; the newest card
  is on top. Both Graveyards are publicly browsable newest-to-oldest.
- Equip Spells will target a Monster and remain in a shared Spell/Trap Zone
  while equipped. Field Spells will remain in a dedicated Field Zone.
- Future hand-activated Spell/Trap effects may explicitly bypass field
  placement; this is an alternate effect rule, not the default flow.

Initial deck direction:

- Shared common cards usable by multiple decks.
- Three sample archetypes: Aggro, Sustain/Defense, and Control.
- Heavy overlap is allowed; 30-card decks do not require 30 unique designs each.
- Combo is optional after the first three archetypes work.

### Monster HP and stat design — approved 3 November

- Base HP is the printed immutable definition. Current maximum HP includes
  active modifiers. Current HP is maximum HP minus accumulated damage, clamped
  to zero. Raising/lowering maximum HP preserves that damage: 600/900 plus
  300 maximum HP becomes 900/1200. Losing a buff can cause KO.
- Recovery restores current HP only, capped at current maximum HP; it cannot
  revive a KO. Legendary Fighter removes all allocated Energy and recovers
  500 HP per Energy, with excess recovery discarded. Tide Sprite instead gains
  200 maximum HP permanently while on the field, preserving damage (10 November revision).
- Reference HP bands: Basic 500–1200; Advanced 1200–2000 (typical 1600);
  Supreme 2000–3000 (typical 2500); Ultimate 3000–4000 (typical 3500).
  These overlap intentionally and are design guidance, not asset-validation limits.
- Ordinary ATK is roughly 50–70% of HP and DEF 20–25%. Specialists and strong
  effects may depart from those ratios: e.g. 1200/800/0 aggro or 1000/0/700 tank.
  Balance effects, attack Energy and tribute investment together, not raw stat sum.
- Do not add unconditional effect-based Monster destruction. Damage effects
  can KO; direct Spell/Trap removal is distinct. Reflection/transfer effects
  require their own approved timing/loop rules before implementation.
- Approved rebalance: Fairy 700/400/150; Imp 800/500/200; Tide 900/450/200;
  Stone 1000/350/450; Fire Dragon 1600/1000/300; Sky Sage 2500/1400/600;
  Legendary Fighter 3500/1000/800 (HP/ATK/DEF). Other base fields unchanged.
  Stone's persistent DEF gain is now 300; Guardian Shift's is 500. See
  `Docs/HP_BALANCE_2026-11-03.md` for verification and acceptance steps.
- Student rebalance, 10 November (supersedes the affected values above): Tide
  600/450/200, +200 maximum HP; Stone 1000/350/300, +100 permanent DEF;
  Sky Sage 2500/1600/600, attack Energy 2, pay 2 Energy to draw 2 cards.
  Legacy sample-generation/balance commands and automatic data rewrites were
  removed; artwork assignment and read-only validation remain available.

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
12. The presentation consumes accepted `ActionResult.Events` in order. Visual
    timing, camera movement, and sound must not alter rules, RNG, or action order.
    Pause human and bot input while a blocking presentation sequence plays.

This preparation supports headless simulation, Android/Windows, replay, save/load, and PvP without implementing all of them immediately.

### Duel presentation MVP

The functional animation slice must show action causality: card movement for
draw, summon, Set, and activation; a visible attack source and target; HP/LP and
Energy changes; destruction/Graveyard movement; phase/turn transitions; and
the order in which chain links resolve. A compact duel log remains readable
when animations are skipped. It should share the same accepted-event stream as
the animation queue. Start with one generic animation per event family rather
than individual effects for each card. See `Docs/DUEL_PRESENTATION_PLAN.md`.

**2 November source-status update:** the first queue is now implemented on the
local board. It captures the rendered board before a valid action, consumes the
accepted `GameEvent` list in order, blocks input while playing, and converges to
the authoritative final `GameState`. The implemented feedback covers opening
hands, draws, summon/Set/activation, Field placement, Graveyard movement,
attack/damage, Monster HP, player LP, Energy, and turn/phase changes. This is a
functional baseline, not final acceptance. The compact Settings Duel Log now
has basic student acceptance; explicit Fast/Skip, response/chain readability,
all-category Play Mode acceptance and Android touch/performance remain pending.

### Product shell and persistence MVP

Start with a guest/local profile and local deck storage. The Main Menu exposes
Deck Building, PvE, and PvP; an unavailable online mode must say so clearly.
Deck Building validates the configured 30–40 card limit and three-copy limit,
saves/selects 2–3 local decks, and includes at least two preset decks. Keep
stable `cardId` references in saves; add a version field and reject or migrate
unknown IDs. Account recovery/cloud sync is a separate online-scope decision.

The 21 October Deck Builder implementation is a separate `AppShell` scene and
must not change the duel layout or core rules. The local catalog uses
existing CardData and card prefabs. Its first UI pass follows a two-layer
portrait layout: a numbered deck grid behind a draggable card-catalog drawer,
six card columns, a text search of card name/effect text, basic category and
stat/type filters, and reversible sorts. The drawer has expanded and collapsed
positions. The detailed filter uses Monster and Spell/Trap tabs; effect-component
filtering, Extra Deck, rarity, and collection economy are not in this game's
current data model. "Newest" is the card asset's recorded edit time at the
last catalog refresh, not a live online release date. The two local presets
must be validated against the catalog and current GameConfig. Guest login is
local only. The subsequent student-approved app-foundation slice connects the
selected saved 30–40-card deck to PvE against the fixed sample opponent deck through a
one-use scene handoff. As of 9 November, Home's Duel mode selector chooses Easy
or Normal policy for that opponent deck; Auto uses the same selected policy for
Player 1. Direct Play of SampleScene retains its original Easy demo.
For the Home-to-PvE route, an Editor repair adds a Camera to `AppShell` and
serializes the existing `DuelBoardReadOnlyDemo` component with its fixed deck
and visual references on `SampleScene/Field_Canvas`. This is the one necessary
scene integration change; it must leave all saved duel RectTransforms and UI
hierarchy unchanged. Both scenes must be enabled in Build Settings, with
`AppShell` first. A scene switch must not rely on an Editor-only Play-mode hook.

The student approved authoring the AppShell UI in Edit Mode. The Editor tool
creates/saves it once; runtime binds serialized references and updates dynamic
card lists, names, counts and results. It must not regenerate layout over manual
scene edits. Home has a top-left avatar/name shortcut to Profile and a top-right
Settings shortcut. Profile supports local name/avatar and recent PvE history.
Settings stores master/music/effects volumes; there is no new music/SFX asset yet.
An app-only result/exit adapter records completed or abandoned matches and
returns to Home without changing duel rules. Auto/hot-seat practice and abandoned
matches are excluded from manual-PvE win rate; this is a reviewable UI/statistics
default, not a gameplay rule. Match archives include deck IDs, seed, version
metadata and accepted actions; they are not yet a verified replay feature.

**2 November source-status update:** the AppShell slice is implemented in the
saved scene and its local persistence/session boundary is present. Focused
Editor verification passed on 21 October for save/load and migration safety,
copy/deck validation, selected 30/31/40-card sessions, Easy Bot decisions, and
authored UI bindings. Deck Builder filter behavior and selected-tab indication
were committed in checkpoint `c6b8b6c`. A Play Mode acceptance
pass remains separate. The first Android APK now builds successfully; physical
device acceptance and the Windows build remain pending.

Local schema v2 migrates v1 decks and adds stable local-player/deck IDs, timestamps,
revision, settings and recent results. Keep the existing save filename and
company/product identity to preserve existing saves. Local data is untrusted and
device-local, not authentication or authoritative online ranking. Future account
linking, per-account cache, secure token storage and server validation are described
in `Docs/APP_FOUNDATION_GUIDE_2026-10-21.md`. No password/backend/network SDK is added.

## 12. Bots and Evaluation

- `IBot` receives a safe game-state view and chooses a legal `GameAction`.
- Easy Bot: fixed rules.
- Normal Bot (implemented 9 November): tactical heuristic scoring for Energy,
  summoning/tributes, positions and effect benefits, with bounded public-board
  attack-order forecasts (up to five follow-up attacks and 256 forecast-node
  budget per candidate). It does not clone/simulate full GameState or train a model.
- Normal's `Observe` adapter copies owned/public definitions, remaining attacks,
  public Graveyards and response context without retaining mutable GameState or
  hidden opponent definitions. The shared BotGameStateView still omits opponent
  Graveyard details; this is supplemented only for Normal, not fixed globally.
- Main Menu Duel mode selects Easy/Normal; Auto delegates both sides to the
  selected policy. Initial Normal-mode Play Mode operation is student-confirmed;
  the updated policy still needs Play Mode acceptance and full all-effect coverage.
- Normal tactical update, 10 November: self-effect/Equip valuation, projected
  tribute ATK, defense when an attack cannot be prepared, high-ATK/rank KO priority,
  situational Energy cost and deterministic tie handling. Core/Easy are unchanged.
  Tactical verification and six automated fixture matches passed; runner build:
  0 errors/0 warnings. Baseline 88/150 wins (58.67%); updated same-input set
  108/150 (72%); unseen-seed set 107/150 (71.33%). All 300 updated matches Finished.
- Student decision: approximately 7:3 is sufficient for the current Normal
  milestone; 8:2 is an optional ideal, not a requirement to keep tuning now.
  Freeze Easy/Normal as reference policies for card expansion, Hard or ML research.
  These results cover two preset Decks; do not claim universal bot strength or
  require each later bot to achieve 80% against its predecessor.
- Hard Bot: limited-depth search such as Minimax/Alpha-Beta **[OPTIONAL AFTER TIER 1]**.
- ML/self-play: optional experiment only, after Normal Bot and the headless
  simulator are reproducible. Train on compact state/action features, not
  rendered frames. On the 8 GB development machine, keep heavy training out of
  the Unity Editor process and use offline or remote runs if attempted.
- Student direction, 10 November: defer Hard Bot and actual AI/ML training until
  card expansion and hard gameplay rules are stable. Preserve the completed
  replay/hash/AI-contract foundation. The student subsequently authorized compatible
  SDK installation first, then room-code PvP MVP design; installation is now verified.
- Deferred workflow: freeze a versioned card/rule catalog and reference policies;
  validate replay/observation privacy after expansion; choose Hard/learning method;
  build the search/training adapter and labeled datasets; separate training/tuning
  seeds from evaluation seeds; train/version the model and compare against fixed
  Easy/Normal plus unseen seeds, recording correctness and decision time.

The headless Windows runner now uses the shared legal-action/apply path and writes
CSV/JSONL, run metadata, selected observation/action traces and paired summaries
under SimulationResults on D. It runs sequentially. Exact experiments are in
Tools/Simulation/TESTING_NOTES.md. Baseline and post-update raw data are local,
ignored outputs, not repository attachments. Reproducible configs are tracked.
Windows/Mono replay/hash/AI-contract MVP implemented 10 November: run/trace
schema 2, read-only full pure-state graph hashes before/after actions, replay
through GameSession, ordered-event/observation/legality checks and first-divergence
reports. Reflection stays in the simulation adapter; Core/rules/bots are unchanged.
AI contract v1 exports masked numeric/categorical observations, local entity/action
indices and per-player next-decision transitions with both terminal rewards.
Private diagnostic hashes/seed/deck metadata are excluded from policy input.
Old schema-1 archives remain unchanged and cannot claim full-state verification.
Same-version compatibility is strict; no UI playback, state-cloning/search API,
trainer/Gym adapter or Android/IL2CPP verification is claimed.
The target metric contract (including the remaining state-hash work) is:

- Winner and matchup.
- Random seed and who went first.
- Turn count.
- Energy spent, allocated, and unused.
- Decision time per move.
- Invalid-action or timeout count.
- Rules/configuration version and final-state hash for reproducibility.

CSV output supports thesis tables and charts. A replay record should also
capture schema version, seed, deck IDs, first player, ordered accepted actions,
and state hashes. Do not let animation playback drive the simulator clock.

## 13. PvP and Networking

### SDK installation checkpoint — 10 November

- Approved scope: create room, share a join code, enter code to play 1v1;
  no matchmaking. Unity must remain 2022.3.62f3; never upgrade it automatically.
- Installed/pinned: Netcode for GameObjects 1.15.1, Relay 1.2.0,
  Authentication 3.6.0, Transport 1.5.0, Services Core 1.15.1, QoS 1.3.2,
  Mono Cecil 1.11.6 and Newtonsoft JSON 3.2.1. Existing Collections 1.2.4,
  Burst 1.8.21 and Mathematics 1.2.6 are pinned without upgrading them.
- PvpSdkVerification PASS at 2026-11-10T07:30:48Z: all selected packages and
  required APIs loaded, scripts compiled, 54 pre-existing package versions and
  ProjectVersion.txt unchanged. Student confirmed Console 0 errors/0 warnings.
  Verification plan/report are in Tools/Simulation/Control/PvpSdk/plan.json and
  status.json; these two checkpoint files and the package lock are tracked explicitly.
- Package downloads initially failed because the Unity login token expired;
  re-login restored dependency resolution. A PackageInfo name collision in the
  diagnostic was fixed with an explicit Package Manager type alias.
- The evaluated unified Multiplayer Services SDK requires Collections 2.2.1;
  standalone Relay avoids that major dependency upgrade. Relay is a legacy SDK;
  reassess service support before production release. No Lobby/Matchmaker added.
- This PASS verifies installation/Editor API availability only. Services were not
  configured, no online duel/Android build was tested, and no online accounts or
  cloud history were implemented. Next: discuss the PvP MVP design, then service
  configuration and a real two-device connectivity test.

- Local guest IDs/profile, recent PvE history and local wins/losses already exist;
  online identity, cross-device recovery, online match history and trusted PvP
  statistics do not. Preserve guest data and map it to authenticated identities
  explicitly; existing offline PvE/Auto results are not rated PvP results.
- Start with a scoped online 1v1 room/create/join-code prototype on two devices,
  then integrate the duel UI. Card expansion can continue later if both players
  use the same negotiated rules/catalog version in each match.
- Define stable account ID versus editable display name, room ID and match ID;
  choose a recoverable login/account-link flow before promising durable accounts.
- Decide the authority/trust model. Host-authoritative Relay is a candidate for
  a friends/demo MVP, not a claim of competitive anti-cheat. Never broadcast full
  GameState, both Decks/hands, seeds or diagnostic private-state snapshots as the
  client synchronization protocol. Send authorized perspective data/events and
  sequence acknowledgments. Simulator reflection hashes are not a ready-made
  Android/network protocol; a dedicated view/protocol adapter is required.
- Define match lifecycle, surrender, disconnect grace, cancellation and host-loss
  handling; record why a match ended. Keep local PvE, practice/Auto and PvP
  statistics separate. Default win rate: wins/completed counted matches (including
  draws); exclude canceled/unfinished matches, and agree forfeits/timeouts first.
- Define a backend-owned result record keyed by matchId, deduplication and retry
  rules before updating both profiles/history. Protected writes alone do not
  prove a host-reported result is true; specify result validation/trust separately.
  Keep private match diagnostics separate from player-visible history.
- SDK installation/API compatibility on Unity 2022.3.62f3 has passed. Next design
  the MVP and verify Android/Windows connectivity, identity recovery, service limits
  and costs before enabling the online flow or promising persistent accounts.
  Current MPS installation guidance is Unity-6-oriented; do not infer compatibility
  or upgrade the Editor automatically: [Unity installation guide](https://docs.unity.com/en-us/mps-sdk/install-and-upgrade).
- Reference constraints: [Relay forwards traffic](https://docs.unity.com/en-us/mps-sdk/networking/relay-servers);
  [anonymous account recovery requires linking](https://docs.unity.com/en-us/authentication/use-anon-sign-in);
  [Cloud Save access classes](https://docs.unity.com/en-us/cloud-save/concepts/player-data).

"Networking solution" means the technology and service used to identify players, create/join matches, carry messages between devices, validate actions, handle disconnects, and optionally save player data.

Selected SDK foundation **[INSTALLED/EDITOR VERIFIED; online behavior pending]**:

- Unity Authentication for player identity. Start with anonymous accounts for testing; add Username & Password or Unity Player Accounts if cross-device recovery is required.
- Standalone Relay SDK allocations and join codes; unified Multiplayer Services SDK Sessions and Lobby are not installed.
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
| KO penalty by rank (confirmed 3 November) | 500 / 1000 / 1500 / 2000 LP; independent of Energy |
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

Checkpoint: **Offline Demo 0.1.0 + Duel Log**, 9 November; previous checkpoint `977a734`.
The offline APK built in the existing Editor with 0 errors and 0 warnings.
Verified package: `com.binhb2105696.strategiccardgame.demo`, version 0.1.0/code 1,
Android API 23 minimum/API 34 target, portrait, IL2CPP, ARMv7 + ARM64. APK signing
verification passed; this is an Android Debug test signature, not a store release.
No Internet permission is requested. Local profile/deck/settings/history round-trip,
backup creation and corrupt-file protection passed against temporary Editor data.
Physical-device installation, full-match/touch/performance and save-after-restart
acceptance remain unreported. Keep the same demo package ID/signing key for updates.
See `Docs/OFFLINE_DEMO_GUIDE.md`; binaries are local ignored build outputs.

Settings > Duel Log is implemented and student-confirmed at a basic Play Mode level.
It records each accepted action and ordered events, including automatic turn-start
actions, freezes separate hidden-info views, and reuses text rows for scrolling.
Privacy, reveal-history, ordering, maximum-HP and duplicate-capture checks passed.
It is in-memory for the current match, not a persisted event journal or post-match
viewer. Further Duel Log standardization, detailed effect/chain review and card
expansion are explicitly deferred by the student. No Google login/cloud sync was added.
The November balance slice implements capped recovery, damage-preserving maximum
HP, rank-based KO penalties, the 6000 LP trial, seven Monster stat changes and
Stone/Guardian DEF adjustments. The student reported HP/KO diagnostic PASS.
The replacement HUD shows current HP only, with a rounded health bar about
one-third of the Monster Zone width and five colors in 20% bands. HP/ATK/DEF
are right-aligned; the bar shares the status layer and attack animation clone.
The student accepted the HUD in Play Mode. The dated HP balance document is a
historical slice; its former current/maximum/no-bar HUD status is superseded.

Normal Bot, Duel mode Easy/Normal selection and matching Auto are implemented.
The Editor test's internal-access issue was fixed without changing Core APIs;
separate runtime/test compilation, three tactical checks and six synthetic-deck
automated matches passed outside Unity. The student confirmed Unity compilation
and initial Normal-mode Play Mode operation. No full card-pool acceptance,
comparative win-rate results or production batch simulator is claimed.
No new archetype, networking or platform-build milestone was completed in this slice.

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
- The fixed playable demo deck contains 30 cards across 17 designs, with Basic
  Monsters prioritized at three copies and higher-rank/Spell/Trap cards using
  smaller copy counts where appropriate. Validation rejects missing cards,
  empty effect definitions, invalid deck size, or more than three copies. An
  Edit Mode bootstrap repairs the sample setup before Play instead of requiring
  a remembered manual menu command.
- The shared `GetLegalActions` view now covers current Main/Battle/End choices,
  exact tribute and ordered target selections, response priority/Pass, and
  optional triggered-effect choices. Every enumerated action is checked by
  `ValidateAction()` before it reaches UI, bot, or future simulator consumers.
- `IBot` now receives an immutable `BotGameStateView` snapshot and the shared
  legal-action list. The view omits the opponent's Hand identities and masks
  face-down opponent cards. The first deterministic Easy Bot scores that list
  and submits its selected action through `GameSession`; a runtime Duel
  Settings toggle assigns Player 2 to the bot while keeping Player 1's
  perspective and hidden opponent hand. The student accepted the Easy Bot in
  Play Mode on 18 October. A separate compact Auto control can temporarily
  assign Player 1 to the same bot as well, enabling a visible bot-vs-bot duel
  without bypassing `GameSession` or `GameRules`.
- The portrait board uses a 1080x1920 Canvas, SafeAreaRoot, separate
  Board/HUD/Overlay layers, four 3-slot rows, and zones with independent
  Border, HitArea, CardAnchor, and Highlight children. Deck, Graveyard, Field,
  Hand, HUD, turn/phase, LP, Energy, and live card state are presented from the
  active player's perspective. Opponent Monster and Spell/Trap Zone indices are
  mirrored for the rotated perspective without changing logical zone indices.
  Runtime presentation now applies the selected 1080x1920 battlefield image
  and player avatars. A nonempty Deck displays the shared card back; each
  Graveyard displays its latest card face-up and updates after state changes.
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

1. Run a focused Play-mode acceptance pass for the newly assigned Monster
   artwork and remaining all-category edge cases: Equip cleanup, Field replacement/maintenance,
   Trap set-turn lockout, attack responses, Counter Trap negation,
   two-or-more-link LIFO resolution, insufficient Energy, full zones, pending
   triggers at match end, and the match-result overlay.
2. Test the produced Android demo on physical devices: portrait readability,
   touch input, full-match flow, performance and local persistence. The basic
   Duel Log is accepted; detailed polish/effect/chain review is deferred.
   Explicit Fast/Skip remains pending for a later presentation slice.
3. Keep the accepted approximately 7:3 Normal checkpoint and headless evaluation
   pipeline as references; broaden experiments only for the next selected scope.
   The Windows replay/state-hash and AI-contract MVP is implemented; broaden its
   coverage as needed before networking/search/training depends on it. Hard/ML
   remain optional and deferred until cards/rules stabilize.
4. Design the scoped room/create/join-code PvP MVP using the installed SDKs
   from section 13, then configure services and test two-device connectivity.
   Keep Unity 2022.3.62f3 unchanged. Remaining AppShell/device acceptance and
   a playable Windows build are still pending.

## 16. Roadmap

### 21-31 October temporary sequence (student-approved direction)

- 21-24 Oct: build and visually review the separate guest/menu/Deck Builder
  slice, local versioned deck saves, two presets, and portrait/Windows framing.
  Convert the app UI to Edit-Mode-authorable views and connect the selected deck,
  result/Home flow, local Profile and Settings through small app adapters. Preserve
  duel layout/core rules. Verify this slice before late-October visual tuning.
- 25-27 Oct: fix only blocking app/duel integration faults, verify the local
  full-match loop, and obtain the planned early Android development build.
  Check Windows portrait behavior in the Game view as well.
- 28-31 Oct: bounded UI/animation corrections and all-category duel acceptance;
  prepare reproducible data and notes for Normal Bot, simulation, replay, and
  the networking feasibility decision due in early November.

This is task resequencing, not a reduction of Tier 1A or Tier 1B. The ~3 Nov,
~10 Nov, ~17 Nov, ~24 Nov, and 25 Nov freeze gates remain. If the app shell or
Android build consumes the time reserved for Normal Bot/network feasibility,
raise that risk with the student rather than silently moving those commitments.
See `Docs/APP_SHELL_HANDOFF_2026-10-21.md` for bounded follow-up work.

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
- Step 6 bot foundation: **Easy and Normal Bot implemented; initial Normal-mode Play Mode operation confirmed 9 November**.
  It consumes an immutable state view plus shared legal actions, submits through
  GameSession, pauses for human response priority, and can be toggled at runtime
  without changing the saved scene. The visible Auto control can assign both
  players to the selected policy for presentation. Duel mode offers Easy/Normal.
  Normal's comparative evaluation, full-effect acceptance and headless metrics
  remain incomplete.

| Step | Target | Content | Verification |
|---|---|---|---|
| 1 | ~6 Oct | CardData/CardView gaps, IDs, icon, derived values, 5-8 test cards, functional card prefabs | Multiple card categories display correctly in Play Mode |
| 2 | ~13 Oct | Pure CardDefinition/core boundary, GameConfig, CardInstance, PlayerState, GameState, DeckData, shuffle/draw/hand | Two players initialize without UI and state prints correctly |
| 3 | ~20 Oct | Turn phases, Energy, serializable GameActions, legal-action/apply path, summon/allocation/position | Scripted turn sequence produces correct deterministic state |
| 4 | ~27 Oct | Complete local duel acceptance, event queue, functional animation and duel log; early Android development build | Full local match reaches a winner and each key action is visually understandable |
| 5 | ~3 Nov | All-category edge pass, Normal Bot/headless runner foundation, replay/state hash, networking feasibility spike | Complete local match with all categories; bot batches start; networking choice discussed |
| 6 | ~10 Nov | Normal Bot metrics, guest profile/Main Menu/PvE entry, Deck Builder MVP and local deck saves | Human-vs-bot, batch metrics, and selection of a valid saved deck work |
| 7 | ~17 Nov | Online host-authoritative PvP and account flow using approved solution | Android/Windows devices complete an online match |
| 8 | ~24 Nov | Android build first, Windows build, regression/balancing; Hard Bot only if Tier 1A is stable | Stable builds and thesis experiment data |
| 9 | 25 Nov-early Dec | Feature freeze, final experiments, report, defense preparation | Final build and thesis materials |

If schedule slips, reduce extra cards, cosmetic polish, Hard Bot, and ML first.
Discuss any change to the Tier 1A/1B commitments with the student; do not
silently remove them. As of 19 October, the core Step 4 local-play slice and the expanded executable
card-flow portion of Step 5 have initial Play Mode acceptance. The complete
legal-action view and first Easy Bot have Human-vs-Bot Play Mode acceptance.
The latest source audit on 2 November confirms a working first presentation
queue and the local AppShell/Deck Builder/PvE slice in addition to the earlier
background, avatar, Deck/Graveyard, Auto, and Monster-artwork work. App
persistence and UI bindings have a focused Editor PASS; presentation and
AppShell still need their stated Play Mode/device acceptance. The immediate
critical path is the all-category integration pass, presentation completion,
replay/state-hash checks, networking feasibility, follow-up evaluation after card changes,
and platform builds. Do not substitute more isolated card scripts or smoke
tests for those milestones. Tests verify each finished slice.

## 17. Change Management

1. Discuss changes to **[CONFIRMED]** rules before implementation.
2. When a rule or default changes, update the relevant section and section 14.
3. Update README only for high-level changes.
4. If code and this document disagree, report the inconsistency instead of silently choosing one.
5. Update progress after each verified step, not merely after code is written.
