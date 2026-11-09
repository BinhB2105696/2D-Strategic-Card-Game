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
- **Online PvP mode** for two players.
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
- **Normal Bot (implemented):** tactical heuristic scoring plus bounded public-board attack-order forecasts; initial Play Mode operation confirmed on 9 November 2026. Comparative strength is not yet measured.
- **Duel mode:** the Main Menu offers Easy/Normal selection. Auto uses the selected difficulty for both players; Auto/practice matches do not count toward manual PvE wins/losses.
- **Advanced Bot:** optional deeper decision-making using search techniques such as Minimax/Alpha-Beta after the core scope is stable.
- **Optional ML extension:** machine-learning/self-play experiments may be investigated after the core game and non-ML bots are stable.

The Advanced Bot and optional ML component must not block completion of the stable core game, Easy/Normal bots, or PvP.

### Bot Evaluation

The game core is planned to run independently from the visual UI so bots can play automated matches for thesis evaluation. Planned metrics include win rate, average turn count, Energy usage, and decision time per move.

## PvP

The project is planned to support online 1v1 PvP with player identity/account support.

The current tentative direction is to evaluate Unity Gaming Services Authentication and Multiplayer Sessions/Relay after the deterministic local game core is stable. No networking package is selected or installed yet.

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
- [ ] Headless bot-vs-bot simulation, replay/state hashes, and evaluation metrics
- [x] Guest profile, Main Menu, and Deck Builder MVP with local deck saves (Editor verified; device acceptance pending)
- [ ] Gameplay balancing and testing
- [ ] Online PvP and player identity/accounts
- [x] First offline Android APK demo 0.1.0 (build/signature verified; physical-device acceptance pending)
- [ ] Windows build
- [ ] Advanced Bot
- [ ] Optional ML experiments
- [ ] UI/UX and visual polish
- [ ] Thesis documentation and final build

## Documentation

Detailed and frequently changing design decisions are maintained separately in:

`Docs/PROJECT_CONTEXT.md`

This document is intended to be updated whenever important gameplay, architecture, bot, or project-scope decisions change.

## Current Status

Updated 9 November 2026. Checkpoint: **Offline Demo 0.1.0 + Duel Log**.
Previous checkpoint: `977a734` (Normal Bot, Duel mode, Auto, compact HP display).
The Android APK built with 0 errors and 0 warnings, is approximately 35.52 MiB,
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
Separate runtime/test compilation and three tactical checks plus six synthetic-deck
automated matches passed outside Unity. After the Editor test assembly fix, the
student confirmed Unity compilation and initial Normal-mode Play Mode operation;
this is not a measured win-rate improvement or full all-card acceptance.

### Important remaining work

- Collect offline-demo device feedback and confirm install, full-match flow and local persistence; card expansion, detailed effect/chain review and Duel Log polish are deferred by the student.
- Complete remaining AppShell/presentation acceptance and explicit Fast/Skip controls when scheduled.
- Evaluate/tune Normal versus Easy across decks, seeds and starting sides; add a headless batch runner and CSV metrics (win rate, turns, Energy, decision time, errors).
- Implement and verify deterministic replay loading and state hashes; existing action archives alone do not complete replay.
- Complete the networking feasibility spike, then online 1v1 PvP and player identity/account flow.
- Validate the produced Android demo on physical devices, then produce a Windows build; include touch/readability/performance and complete-match checks.
- Finish regression/balance work, reproducible thesis experiments, report and final build before defense. Hard/Advanced Bot and ML remain optional.

The revised scope and current gaps are in
`Docs/PROJECT_CONTEXT.md` and `Docs/DUEL_PRESENTATION_PLAN.md`.

The December thesis schedule and dated milestones are in
`Docs/PROJECT_CONTEXT.md`. The dated `Docs/HANDOFF_2026-10-07.md` records the
earlier integration state; use the newer progress section in Project Context
when they differ. Preserve the Tier 1 scope unless the student explicitly
changes it.

Detailed gameplay rules, current design decisions, implementation progress, and development constraints are maintained in:

`Docs/PROJECT_CONTEXT.md`
