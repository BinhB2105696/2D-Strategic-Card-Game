# 2D Strategic Card Game

> **Graduation Thesis Project – Can Tho University**

## Project Information

- **Project title:** Xây dựng trò chơi thẻ bài chiến thuật 2D với hệ thống bot và chế độ chơi PvE, PvP
- **English title:** Development of a 2D Strategic Card Game with Bot System and PvE/PvP Modes
- **Target Platforms:** Android first / Windows second
- **Engine:** Unity
- **Programming language:** C#
- **Student:** Nguyễn Thanh Bình
- **Student ID:** B2105696
- **Institution:** Can Tho University (CTU)
- **Project status:** Core rules established; playable duel integration in progress

## Overview

This project is a 2D strategic card game developed as a graduation thesis.

Players use cards with different attributes, statistics, effects, and strategic roles to build a playable strategy and defeat an opponent. The game is designed around two-player matches and aims to provide both single-player and multiplayer experiences.

The project is planned to include:

- **PvE mode** against multi-tiered bots with different decision-making approaches.
- **Online PvP mode** for two players (prototype retained; deferred until the offline priorities are complete and time permits).
- A compact Deck Builder and local/guest profile for selecting playable decks.
- A modular card system that can be extended with additional cards and effects.
- A turn-based combat system with strategic resource management.
- Multiple strategic deck/archetype styles.
- A data-driven architecture to make cards easier to create, balance, and maintain.

## Planned Gameplay

A match is designed for approximately **10–15 minutes**.

Each player has:

- HP
- Hand
- Deck
- Graveyard
- Energy/resource
- Monster zones
- Spell/Trap zones

The initial board design is planned around:

- 3 Monster Zones
- 3 Spell/Trap Zones

### Turn Structure

A turn is currently divided into five phases:

1. **Recovery Phase**
   - Resolve the Draw Step.
   - Resolve the Energy Refresh Step independently.

2. **Standby Phase**
   - Resolve start-of-turn, maintenance, and Standby effects.

3. **Main Phase**
   - Summon monsters.
   - Change monster position when allowed.
   - Activate spells and effects.
   - Allocate resources when applicable.

4. **Battle Phase**
   - Attack with eligible monsters.
   - Resolve combat against Attack or Defense position.
   - Resolve battle-related effects.

5. **End Phase**
   - Resolve end-of-turn effects.
   - Finish the current turn.

> Gameplay values and detailed rules are subject to change during development and balancing.

## Card System

The project is designed around a data-driven card architecture.

Conceptually:

```text
CardData
   ↓
CardInstance
   ↓
CardView
```

- **CardData:** static information such as name, type, stats, artwork reference, and effects.

```text
  CardData
  │
  ├── Card Name
  │
  ├── Category
  │ ├── Monster
  │ ├── Spell
  │ └── Trap
  │
  ├── Attribute
  │ ├── Metal
  │ ├── Wood
  │ ├── Fire
  │ ├── Water
  │ ├── Earth
  │ └── Wind
  │
  ├── Card Type
  │ ├── Monster
  │ │ ├── Basic
  │ │ ├── Advanced
  │ │ ├── Supreme
  │ │ └── Ultimate
  │ │
  │ ├── Spell
  │ │ ├── Normal
  │ │ ├── Equip
  │ │ └── Field
  │ │
  │ └── Trap
  │ ├── Normal
  │ └── Counter
  │
  ├── Monster Stats
  │ ├── HP
  │ ├── ATK
  │ └── DEF
  │
  ├── Spell/Trap
  │ ├── Energy Cost
  │ └── Speed
  │
  └── Effect Text
```

- **CardInstance:** runtime state of a card during a match.
- **CardView:** Unity UI/presentation layer.

The goal is to allow new cards to be added mainly through data and reusable effects instead of creating completely separate code for every card.

### Planned Card Types

- Monster
- Spell
- Trap

### Planned Reusable Effects

Examples include:

- Damage
- Heal
- Draw
- Attack/Defense buff
- Attack/Defense debuff
- Spell/Trap removal (Monster defeat must respect HP)
- Discard
- Change position
- Recover/revive
- Summon
- Gain energy
- Remove energy

Triggers and conditions are intended to be separated from effects so that different cards can reuse the same gameplay components.

## Tentative Architecture

The planned architecture separates game logic from Unity presentation:

```text
GAME DATA
├── CardData
├── CardEffectData + reusable effect components
└── DeckData
      ↓
GAME LOGIC
├── GameState
├── PlayerState
├── TurnManager
├── CombatSystem
├── GameRules
└── Action System
      ├── SummonAction
      ├── AttackAction
      ├── ActivateCardAction
      ├── ChangePositionAction
      └── ...
      ↓
UI / GAME OBJECTS
├── CardView
├── Board
├── Hand UI
├── Graveyard UI
└── Game HUD
```

A major development principle is to keep the core game rules independent from the visual UI as much as practical.

## Bot System

The PvE system is designed to support multiple levels of bot behavior.

The planned interface is:

```text
GameState
    ↓
Available Actions
    ↓
Bot
    ↓
Selected Action
    ↓
Game Rules
    ↓
New GameState
```

### Bot Levels

- **Easy Bot (implemented):** deterministic rule-based decisions from legal actions.
- **Normal Bot (implemented):** tactical heuristic scoring plus bounded public-board attack-order forecasts. The accepted reference won 108/150 matches (72%) against Easy and 107/150 (71.33%) on unseen seeds, using the evaluated preset decks; these are not universal strength claims.
- **Duel mode:** the Main Menu offers Easy/Normal selection. Auto uses the selected difficulty for both players; Auto/practice matches do not count toward manual PvE wins/losses.
- **Hard Bot (planned):** advanced decision-making after offline presentation and the expanded card/rule catalog are stable; the search method remains to be selected.
- **ML/AI experiments (planned research direction):** prepare reproducible data and an evaluation protocol, then select and train a policy after the card/rule version is frozen. No trained model exists yet.

The 11 November direction prioritizes a stable offline game, diverse strategic decks, Easy/Normal/Hard bot comparisons and ML/AI research preparation. PvP and online accounts come last if time permits; training must not destabilize the playable offline baseline.

### Bot Evaluation

The implemented headless runner supports automated bot matches and CSV/JSONL results; replay/state hashes and a versioned AI data contract provide a Windows/Mono verification foundation. Extend evaluation to versioned strategic decks, paired seeds with swapped seats/first player, held-out evaluation seeds, win/draw rates, turn counts, Energy usage, decision time and resource use. Preserve fixed Easy/Normal references and keep training/tuning data separate from final evaluation.

## PvP

**Deferred — student decision, 11 November 2026:** retain the room-code PvP prototype, but schedule further online/account work last, only if time remains. This is a development-priority change, not a change to the registered thesis title; confirm any formal scope reduction with the supervisor.

The prototype supports create/join by room code without matchmaking. The student reached a duel between Editor and Android, but reported that effects and card movement were below the offline experience; presentation is not accepted as complete. PvP shares the rules and many visual components with PvE, but still uses a separate snapshot-based presentation coordinator rather than the full PvE event queue. Future work should unify the presentation contract/ordered events before further online polish. The verified SDK pins remain Netcode for GameObjects 1.15.1, Relay 1.2.0, Authentication 3.6.0 and Transport 1.5.0 on Unity 2022.3.62f3; do not upgrade Unity or dependencies automatically. Recoverable online accounts/cloud saves and trusted cross-device results remain unfinished.

## Planned Strategic Archetypes

The initial design considers several strategic styles:

- **Aggro:** focuses on pressure and fast damage.
- **Sustain/Defense:** focuses on survival and resource efficiency.
- **Control/Disruption:** focuses on limiting or disrupting the opponent.
- **Combo:** focuses on interactions between multiple cards and effects.

The exact number of decks and cards will be determined during development and balancing.

## Development Principles

1. **Finish the core game before advanced features.**
2. **Keep gameplay logic modular and testable.**
3. **Prefer reusable systems over card-specific hard-coded logic.**
4. **Avoid making optional AI/ML work a dependency of the main project.**
5. **Add new cards through data and reusable effects whenever possible.**
6. **Document important design decisions and changes.**
7. **Keep the project manageable for the thesis timeline.**

## Project Structure

The Unity project is expected to evolve toward a structure similar to:

```text
/
├── Assets/
│   ├── Art/
│   ├── Audio/
│   ├── Data/
│   ├── Prefabs/
│   ├── Scenes/
│   ├── Scripts/
│   └── UI/
├── Docs/
│   └── PROJECT_CONTEXT.md
├── Packages/
├── ProjectSettings/
├── .gitignore
└── README.md
```

This structure may change as implementation progresses.

## Development Roadmap

- [x] Repository and project setup
- [x] Unity project initialization
- [x] Initial card UI prototype
- [x] Basic project architecture
- [x] Card data system
- [x] Card UI/presentation
- [x] Game state and player state
- [x] Basic turn and phase flow
- [x] Core Energy/resource system
- [x] Basic Monster summon and tribute system
- [x] Basic combat and winner detection
- [x] Reusable effect data and Normal Spell activation flow
- [x] Basic Graveyard movement and Recovery Phase steps
- [x] Portrait board layout and seeded match state preview
- [x] Turn-specific phase/step skip rules and reusable next-opponent-Draw operation
- [x] Equip/Field Spell, Normal/Counter Trap, targeting, and simple chain foundation
- [x] Playable board UI and core local hot-seat match
- [x] Fixed 30-card playable sample deck with a three-copy limit
- [x] Easy Bot and Human-vs-Bot Play Mode flow
- [x] Visible Auto duel, portrait background/avatars, and Deck/Graveyard card presentation
- [x] Event-driven duel-presentation queue foundation (full presentation/device acceptance pending)
- [x] In-match Settings > Duel Log (basic student acceptance; frozen hidden-info views and scrollable history)
- [ ] Explicit Fast/Skip controls and further Duel Log polish
- [x] Normal Bot, Easy/Normal Duel mode selection, and matching Auto policy (initial Play Mode operation confirmed)
- [x] November HP/KO verification and current-HP HUD with rounded five-color health bar (student accepted)
- [x] Headless bot-vs-bot runner, CSV/JSONL metrics and paired Easy/Normal evaluation
- [x] Windows/Mono headless replay verification, full pure-state graph hashes and versioned AI data contract (MVP)
- [x] Pinned PvP SDK verification and room-code duel prototype (Editor/Android connection reported; presentation not accepted)
- [x] Guest profile, Main Menu, and Deck Builder MVP with local deck saves (Editor verified; device acceptance pending)
- [ ] Gameplay balancing and testing
- [ ] Finished online PvP and player identity/accounts (deferred to last, time permitting)
- [x] First offline Android APK demo 0.1.0 (build/signature verified; physical-device acceptance pending)
- [ ] Windows build
- [ ] Hard Bot after expanded cards/rules stabilize
- [ ] ML/AI data preparation, training experiment and comparative thesis evaluation
- [ ] Offline in-match UI/animation polish (immediate priority)
- [ ] Expanded cards and diverse strategic decks with balance/interaction checks
- [ ] Thesis documentation and final build

## Documentation

Detailed and frequently changing design decisions are maintained separately in:

`Docs/PROJECT_CONTEXT.md`

This document is intended to be updated whenever important gameplay, architecture, bot, or project-scope decisions change.

## Current Status

Updated 11 November 2026. Current direction: **offline UI/animation first, card/deck diversity next, then Hard Bot and reproducible ML/AI research**. PvP/accounts are deferred until last if time permits. The connected PvP prototype is retained, but its presentation has not met student expectations; this documentation update does not implement new gameplay or training.
Latest pushed checkpoint: **Windows replay/state hashes and AI data contract MVP, plus verified PvP SDK installation** (`f51a4e6`). The earlier Normal tactical/evaluation checkpoint is `bd5d71d`; later PvP work is not described as a pushed checkpoint.
Earlier checkpoints include `8425b84` (runner acceptance handoff), `0027715`
(headless runner), and `977a734` (Normal Bot, Duel mode, Auto, compact HP display).
The original offline 0.1.0 Android APK built with 0 errors and 0 warnings, is approximately 35.52 MiB,
supports Android 6.0+ and ARMv7/ARM64, and has a verified test signature.
Local profile/deck/settings/history save checks passed in the Editor; Android
install, touch/performance and persistence after app restart still need device testing.
See [offline demo instructions](Docs/OFFLINE_DEMO_GUIDE.md) for output paths,
installation, data retention, signing and the APK checksum. APK/ZIP files stay
under ignored `Build/OfflineDemo/`; they are not stored in source Git history.

The 3 November balance slice uses 6000 starting LP, capped Monster recovery,
damage-preserving maximum HP and KO penalties of 500/1000/1500/2000 LP by rank,
replacing the Energy-based penalty. Seven existing Monsters and two DEF buffs
are rebalanced; no new card designs are added. The student reported the HP/KO
diagnostic PASS and accepted the revised in-match display. Monster HP now shows
only current HP, with a rounded bar about one-third of a Monster Zone's width.
The bar changes red/orange/yellow/light green/bright green in 20% bands;
HP/ATK/DEF are right-aligned and the bar travels with the attack status overlay.
`Docs/HP_BALANCE_2026-11-03.md` records the earlier balance slice, not the latest HUD acceptance.

The repository and Unity project use Unity 2022.3.62f3 LTS.

As of 19 October 2026, the local hot-seat match supports the Monster combat
loop plus playable Normal and Equip Spells, one Field Spell per player, Set
Normal/Counter Traps, targeted and triggered effects, legal-response
filtering, and a simple Speed 1/2/3 last-in-first-out chain. Response prompts
are skipped when a player has no legal response. The fixed 30-card sample deck
contains 17 card designs with no more than three copies of one design. Edit
Mode prepares or repairs its required sample data before Play Mode.

The core now exposes one complete legal-action list for the UI and bots. An
initial deterministic Easy Bot selects only from that list and submits every
choice through the same GameSession/GameRules path as a human. Runtime Duel
Settings can switch Player 2 between the selected bot and local hot-seat control. The
student has accepted the Human-vs-Easy-Bot flow in Play Mode. A compact Auto
button can additionally hand Player 1 to the same legal-action bot so both
sides play while the board remains visible. The runtime board also uses the
selected portrait background and avatars, shows a card back while each Deck is
nonempty, and shows the latest Graveyard card face-up.

As of 2 November 2026, the first event-driven duel-presentation queue is also
implemented in the local board. It captures the board before an accepted action,
plays ordered `GameEvent` feedback for opening draws, draws, summons, sets,
activations, attacks, damage, Energy/HP/LP changes, Graveyard movement, and
turn/phase transitions, blocks input while it is playing, then renders the
authoritative final state. Settings > Duel Log now records accepted actions and
ordered events for human, bot and automatic Recovery steps. Frozen per-player
text hides opponent draws/sets without revealing old private entries later.
The student confirmed basic Play Mode operation; data checks passed for privacy,
ordering, HP changes and capture without duplicates. The log is match-local;
it is not persisted as event history or available as a post-match log viewer.
Explicit Fast/Skip, all-category presentation acceptance, and Android
performance/touch acceptance remain incomplete.

A separate AppShell now provides a local guest profile, local deck saves,
Profile/Settings, Deck Builder, two validated preset decks, and a selected-deck
PvE handoff against Easy or Normal Bot through the Main Menu's Duel mode selector.
Its original focused Editor verification passed on 21
October for save/load safety, deck validation, selected-deck sessions, Easy Bot
decisions, and authored UI bindings. This is local-only: it is not online
authentication, cloud sync or replay playback. The first locally distributed
Android test APK now exists; it is not a store release or device-accepted build.

Normal Bot now scores Energy allocation, tributes, positions and effect benefits,
and forecasts a bounded sequence of public-board attacks. This is not a full
GameState simulation or trained model. Its dedicated observation supplements the
shared bot view with owned/public effect data, remaining attacks and public
Graveyards; the shared view itself still omits opponent Graveyard identities.
The 10 November tactical update fixes self-effect/Equip valuation, accounts for
ATK gained from tributes, prefers defense when no attack can be prepared, values
high-ATK/high-rank targets, and adjusts Energy cost by opportunity. Matching Auto
uses the same policy. Focused tactical checks plus six synthetic matches passed;
the Windows runner built with 0 errors/0 warnings. Core rules and Easy were unchanged.

On the same 150 match inputs, Normal improved from 88 wins (58.67%) to 108 (72%).
A separate 150-match set using new seeds yielded 107 wins (71.33%). All 300 updated
matches finished without errors/truncation; CSV/JSONL and retained traces matched.
Both sets cover two preset decks, three Deck pairings and swapped bot sides;
these are not 300 independent seeds or acceptance of every card/effect.
The student accepts approximately 7:3 as the current Normal milestone; 8:2 remains
an optional research target. Keep Easy/Normal fixed as reference opponents for later work.
See [experiment notes](Tools/Simulation/TESTING_NOTES.md) for exact seeds, paths,
group results and reproduction commands. Large raw results remain local on D.
The existing offline-demo APK predates this update; rebuild it before testing the
new balance/Normal on Android. Updated Normal/Auto Play Mode acceptance is pending.

### Important remaining work

- First polish offline in-match UI, input locking and the ordered animation/feedback for draw, summon, attacks, damage, destruction and effect/chain resolution. Verify a readable full-match flow on Editor and Android; retain the accepted basic Duel Log, with further polish scheduled separately.
- Expand cards and usable decks with distinct strategic roles and interactions; check balance and representative effect/chain cases, then freeze a versioned card/rule catalog for experiments.
- Preserve the accepted Easy/Normal references and the approximately 7:3 Normal results. Build and evaluate Hard after the expanded rules/catalog stabilize; do not require a fixed win-rate gap between every bot tier.
- Extend the existing runner/replay/hash/AI contract for the chosen search/training method. Separate training/tuning and evaluation seeds/decks; record versions, reproducible commands, outcomes, decision time and resource use for the thesis. Training has not started.
- Validate offline Android touch/readability/performance, saves and complete matches; prepare a Windows build and report/final-build evidence.
- Leave PvP, online accounts, recovery/cloud history and trusted online results until the offline/research priorities are complete and time permits. Preserve the prototype and Unity 2022.3.62f3; agree a shared presentation/event contract before resuming online work.

The revised scope and current gaps are in
`Docs/PROJECT_CONTEXT.md` and `Docs/DUEL_PRESENTATION_PLAN.md`.

The December thesis schedule and dated milestones are in
`Docs/PROJECT_CONTEXT.md`. The dated `Docs/HANDOFF_2026-10-07.md` records the
earlier integration state; use the newer progress section in Project Context
when they differ. Preserve the Tier 1 scope unless the student explicitly
changes it.

Detailed gameplay rules, current design decisions, implementation progress, and development constraints are maintained in:

`Docs/PROJECT_CONTEXT.md`
