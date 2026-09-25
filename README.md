# 2D Strategic Card Game

> **Graduation Thesis Project – Can Tho University**

## Project Information

- **Project title:** Xây dựng trò chơi thẻ bài chiến thuật 2D với hệ thống bot và chế độ chơi PvE, PvP
- **English title:** Development of a 2D Strategic Card Game with Bot System and PvE/PvP Modes
- **Target Platforms:** Windows / Android
- **Engine:** Unity
- **Programming language:** C#
- **Student:** Nguyễn Thanh Bình
- **Student ID:** B2105696
- **Institution:** Can Tho University (CTU)
- **Project status:** Core card system development

## Overview

This project is a 2D strategic card game developed as a graduation thesis.

Players use cards with different attributes, statistics, effects, and strategic roles to build a playable strategy and defeat an opponent. The game is designed around two-player matches and aims to provide both single-player and multiplayer experiences.

The project is planned to include:

- **PvE mode** against bots with different levels of decision-making.
- **PvP mode** for two players.
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

A turn is currently divided into four phases:

1. **Draw & Energy**
   - Draw cards.
   - Restore/reset the player's energy according to the current rules.
   - Resolve start-of-turn effects.

2. **Main Phase**
   - Summon monsters.
   - Change monster position when allowed.
   - Activate spells and effects.
   - Allocate resources when applicable.

3. **Battle Phase**
   - Attack with eligible monsters.
   - Resolve combat against Attack or Defense position.
   - Resolve battle-related effects.

4. **End Phase**
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
- Destroy
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
├── EffectData
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

### Planned Bot Levels

- **Easy Bot:** simple rule-based decisions.
- **Normal Bot:** heuristic-based decisions using the current game state.
- **Advanced Bot:** deeper decision-making using search/heuristics where feasible.
- **Optional ML extension:** machine-learning/self-play experiments may be investigated after the core game and non-ML bots are stable.

The optional ML component must not block completion of the core game.

## PvP

The project is planned to support a two-player PvP mode.

The exact implementation approach will be determined after the core single-player game is stable.

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
- [ ] Basic project architecture
- [ ] Card data system
- [ ] Card UI/presentation
- [ ] Game state and player state
- [ ] Turn and phase system
- [ ] Energy/resource system
- [ ] Monster summon system
- [ ] Combat system
- [ ] Spell/Trap system
- [ ] Graveyard/recovery mechanics
- [ ] Initial cards and decks
- [ ] Easy Bot
- [ ] Normal Bot
- [ ] Gameplay balancing and testing
- [ ] PvP
- [ ] Advanced Bot
- [ ] Optional ML experiments
- [ ] UI/UX and visual polish
- [ ] Thesis documentation and final build

## Documentation

Detailed and frequently changing design decisions are maintained separately in:

`Docs/PROJECT_CONTEXT.md`

This document is intended to be updated whenever important gameplay, architecture, bot, or project-scope decisions change.

## Current Status

The repository and Unity project have been initialized using Unity 2022.3 LTS.

The current development focus is the core card system. An initial UI prototype for cards has been created using Unity UI, TextMeshPro, Prefabs, and Prefab Variants. The project is now beginning the implementation of the CardData system and other core gameplay foundations.

Detailed gameplay rules, current design decisions, implementation progress, and development constraints are maintained in:

`Docs/PROJECT_CONTEXT.md`
