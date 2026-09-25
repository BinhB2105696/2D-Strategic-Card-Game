# PROJECT CONTEXT

## 1. Purpose

This document is the current working context for the 2D Strategic Card Game graduation thesis project.

It records the current project scope, gameplay decisions, technical direction, development progress, and constraints.

The design is iterative. Values and rules marked as tentative may change after implementation and playtesting.

README.md is used for the public project overview.
This file is used for detailed development context and AI-assisted development.

---

## 2. AI Assistant Rules

When assisting with this project:

1. Read the existing project structure and relevant files before proposing significant architectural changes.

2. Do not modify, create, rename, move, or delete project files unless the user explicitly authorizes the change.

3. Reading and analyzing project files is allowed when access is available.

4. Before making a proposed code change, explain:
   - what should change,
   - why it should change,
   - which files would be affected.

5. If the user asks for code but does not explicitly authorize direct file editing, provide the code and instructions instead of modifying the project.

6. Never delete or overwrite existing working code merely because another architecture appears cleaner.

7. Do not perform large refactors without explicit approval.

8. Do not change established gameplay rules silently. If a rule appears problematic, inconsistent, difficult to implement, or difficult to balance, explain the issue and discuss it with the user first.

9. Distinguish clearly between:
   - confirmed design decisions,
   - tentative values/rules,
   - proposed ideas.

10. Do not treat README.md or an old discussion as more authoritative than newer decisions recorded in this document.

11. Prefer simple and understandable solutions appropriate for a graduation thesis. Avoid unnecessary design patterns, abstraction layers, frameworks, or premature optimization.

12. When introducing a new C# or Unity concept, explain its purpose because the project is also being used to learn Unity and C#.

13. Do not add packages, plugins, SDKs, external dependencies, or Unity packages without explaining why they are necessary and receiving approval.

14. Do not change Unity version, render pipeline, build target configuration, package versions, or project-wide settings without approval.

15. Do not modify generated/cache folders such as Library, Temp, Logs, obj, or build artifacts as part of source-code development.

16. Never expose, commit, or place API keys, passwords, tokens, credentials, or other secrets in source files.

17. Before operations that could affect many files, Git history, project settings, scenes, prefabs, or serialized Unity assets, stop and request explicit approval.

18. Preserve Git history. Do not force-push, rewrite history, reset destructive changes, or delete branches unless explicitly requested.

19. Prefer small, reviewable changes that can be tested and committed independently.

20. When unsure about an important design decision, ask rather than assume.

---

## 3. Project Goal

Build a 2D strategic card game using Unity and C# for a graduation thesis.

Target platforms:
- Android
- Windows

Main planned modes:
- PvE Easy Bot
- PvE Normal Bot
- PvP 1v1

Advanced bot techniques and machine-learning/self-play experiments are optional extensions and must not block completion of the core game.

The priority is to produce a stable and demonstrable game before attempting advanced bot features.

---

## 4. Development Constraints

Unity version:
- Unity 2022.3.62f3 LTS

Primary programming language:
- C#

Development machine:
- AMD Ryzen 5 5500U
- 8 GB RAM
- Integrated Radeon Graphics
- Windows 10 Enterprise LTSC

The project must remain practical for this hardware.

Avoid unnecessarily heavy tools, packages, rendering systems, and workflows.

Expected thesis defense:
- Early December 2026

Current target:
- Have the main required product substantially completed within approximately two months from 25 September 2026.

---

## 5. Minimum Thesis Scope

The minimum target is:

- Complete playable card game.
- Stable implementation of the main rules.
- Functional user interface.
- Easy rule-based bot.
- Normal heuristic-based bot.
- 1v1 PvP between players/accounts.
- Android build.
- Windows build.

Optional after the minimum scope is stable:

- Advanced/harder bot.
- Deeper search algorithms.
- Machine-learning/self-play experiments.
- Additional cards/decks.
- Additional visual and gameplay polish.

---

## 6. Match Fundamentals

The game is designed for two players.

Current tentative values:

- Player starting HP: 4000.
- Deck size: approximately 30–40 cards.
- Starting hand: 4 cards.
- Normal draw: 1 card per turn.
- Monster Zones: 3.
- Spell/Trap Zones: 3.
- Target match duration: approximately 10–15 minutes.

Primary win condition:
- Reduce the opponent's HP to 0.

Deck-out is currently not an automatic loss condition.

These numerical values are subject to balancing and playtesting.

---

## 7. Turn Structure

A turn currently consists of four phases.

### 7.1 Draw & Energy Phase

- Draw 1 card.
- Restore/reset the player's available base Energy to 5.
- Resolve effects that occur during this phase.

Unused normal Energy is not carried over from the previous turn.

Card effects may modify, restore, or increase available Energy.

The exact Energy values remain subject to balancing.

### 7.2 Main Phase

Possible actions include:

- Summoning monsters.
- Allocating Energy to monsters.
- Changing eligible monster battle positions.
- Tribute/advanced summoning when applicable.
- Equipping cards.
- Activating normal effects.
- Activating Spell cards where allowed.

The current Normal Summon limit is NOT permanently finalized.

Earlier design used one normal Basic Monster summon per turn.
A less restrictive summon rule may be tested if gameplay becomes too slow.

Do not permanently choose either rule without playtesting/discussion.

A monster summoned during the current turn cannot normally change its battle position unless an effect allows it.

A monster already on the field before the turn may normally change its battle position once during the Main Phase.

### 7.3 Battle Phase

Only monsters in Attack Position may normally attack.

A monster normally attacks once unless a card effect changes this.

Against an Attack Position monster:
- Attack damage is applied directly to the target monster's HP according to the combat rules.

Against a Defense Position monster:
- DEF reduces incoming damage.
- If attacker ATK does not exceed the defender's applicable DEF, the defender normally receives no battle damage unless an effect states otherwise.

When a monster's HP reaches 0, it is sent to the Graveyard.

If the opponent controls no monsters, direct attacks against player HP are possible.

Some battle-related effects, Spells, or Traps may activate when their activation conditions are satisfied.

### 7.4 End Phase

- Resolve effects that expire or activate at the end of the turn.
- Finish the current turn.
- Pass control to the opponent.

---

## 8. Monster System

Monsters currently have:

- Name
- Attribute
- Rank
- Artwork
- HP
- ATK
- DEF
- Effect text
- Energy-related attack requirements where applicable

Current Monster Attributes:

- Metal
- Wood
- Fire
- Water
- Earth
- Wind

Possible future additions include:

- Dark
- Light
- Thunder
- Sound

Do not add them until needed.

Current Monster Ranks:

- Basic
- Advanced
- Supreme
- Ultimate

A monster may be placed in:

- Attack Position
- Defense Position

Energy may be assigned to monsters.

Monster attacks or effects may require particular Energy levels.

Whether an effect consumes Energy depends on the individual card/effect and is not a universal rule.

---

## 9. Spell and Trap System

Current card categories:

- Monster
- Spell
- Trap

Initial Spell types:

- Normal Spell
- Equip Spell
- Field Spell

Initial Trap types:

- Normal Trap
- Counter Trap

More types may be added later only when gameplay requires them.

Spell and Trap cards use common Spell/Trap attribute icons in the card UI.

Their specific type is displayed in the Card Type area.

### Speed / Chain

Current intended system:

- Spells are Speed 1.
- Speed 1 effects cannot chain to another activation; they can start a chain when otherwise legal.
- Normal Traps are Speed 2.
- Counter Traps are Speed 3.
- Speed 3 is the highest current speed.
- Only another Counter Trap/Speed 3 response may chain to a Counter Trap.

The detailed chain-resolution implementation has not yet been built and may require refinement.

Do not introduce a complex chain architecture until the basic Spell/Trap system is functional.

---

## 10. Card UI

Cards are implemented using Unity UI under a Canvas.

Primary components:

- Image
- TextMeshPro

Cards are not currently implemented as SpriteRenderer-based world objects.

The general card layout contains:

1. Card Name
2. Attribute icon
3. Card Type
4. Artwork
5. HP
6. ATK
7. DEF
8. Effect/description area

Monster cards display HP, ATK, and DEF.

Spell/Trap cards do not require the Monster combat-stat area and may later use that space differently if Cost, Speed, or other information needs to be displayed.

The current UI is a prototype and is not the final visual design.

---

## 11. Card Prefab Direction

Current intended structure:

Card_Base
- Name
- Attribute
- Card Type
- Artwork
- Effect/description

Card_Monster
- Prefab Variant of Card_Base
- Adds HP
- Adds ATK
- Adds DEF

Card_SpellTrap
- Prefab Variant of Card_Base
- Does not use Monster HP/ATK/DEF
- May later display Cost/Speed if required

Do not create separate duplicated card frameworks unless a concrete implementation problem requires it.

---

## 12. Card Data Direction

Current conceptual separation:

CardData
    ↓
CardInstance
    ↓
CardView

### CardData

Represents the definition/static data of a card.

Examples:

- Name
- Category
- Attribute
- Rank/type
- Base HP
- Base ATK
- Base DEF
- Artwork reference
- Effect-related definition/data

CardData is currently intended to use Unity ScriptableObject.

### CardInstance

Represents the runtime state of one card during a match.

Examples:

- Current HP
- Current ATK
- Current DEF
- Energy currently assigned
- Buffs/debuffs
- Current battle position
- Temporary states

Do not modify shared CardData assets to represent runtime damage or temporary state.

### CardView

Represents the visual/UI presentation of a card.

It displays information supplied by card data/runtime state.

Game rules should not depend directly on UI text or visual components.

---

## 13. Effect System Direction

The long-term goal is to reuse common gameplay operations where practical.

Examples:

- Damage
- Heal
- Draw
- Buff
- Debuff
- Change battle position
- Gain Energy
- Remove Energy
- Summon
- Recover/revive
- Send card to Graveyard

However, the effect architecture is NOT finalized.

Do not create a large generic effect framework prematurely.

Implement simple effects first and refactor into reusable components when repeated patterns become clear.

---

## 14. Bot Direction

Easy Bot:
- Rule-based.
- Uses predefined decision rules.

Normal Bot:
- Heuristic-based.
- Evaluates available actions/game state.
- May consider limited future actions where feasible.

Advanced Bot:
- Optional.
- May use deeper search or more complex heuristics.

Machine learning/self-play:
- Optional experimental extension only.
- Must not delay the core game.

The game rules should ideally expose legal actions in a way that can later be used by both human players and bots.

---

## 15. PvP Direction

PvP is planned as 1v1 multiplayer.

The networking/backend solution is not finalized.

Do not choose or install a networking framework until the core local gameplay and game-state rules are sufficiently stable.

---

## 16. Current Progress

Completed or explored:

- GitHub repository created.
- Unity project initialized.
- Unity 2022.3.62f3 configured.
- Visual Studio 2022 configured for Unity.
- Android build support/tooling installed.
- Initial Unity concepts studied:
  - Scene
  - Hierarchy
  - Inspector
  - GameObject
  - Component
  - Transform
  - RectTransform
  - Canvas
  - Image
  - TextMeshPro
  - Anchor/Pivot
  - Canvas Scaler
  - EventSystem
  - Prefab
  - Prefab Variant
- Initial card UI prototype created.
- Card Base / Monster Variant direction explored.
- Assets/Scripts/CardData.cs created as the first C# data-system experiment.

Current CardData.cs is NOT considered final.

---

## 17. Immediate Development Priority

The next major objective is the core card system.

Proceed incrementally:

1. Inspect the existing CardData.cs.
2. Finalize only the minimum CardData fields needed for the first playable cards.
3. Learn the relevant C# concepts while implementing them.
4. Create test CardData assets.
5. Connect CardData to CardView.
6. Verify that a card prefab can display data correctly.
7. Introduce CardInstance when runtime state is actually needed.
8. Build core turn/gameplay systems incrementally.

Do not generate the entire game architecture at once.

---

## 18. Change Management

Whenever an important decision changes:

1. Discuss the change.
2. Determine whether it is:
   - experimental,
   - tentative,
   - confirmed.
3. Update this document if the change affects project-wide understanding.
4. Update README.md only when the public/high-level project description changes.
5. Keep implementation and documentation consistent.

When code and documentation disagree, do not silently assume which is correct. Report the inconsistency and ask for clarification if it affects implementation.