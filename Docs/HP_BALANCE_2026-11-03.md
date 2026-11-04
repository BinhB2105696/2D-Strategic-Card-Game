# HP / KO balance — 3 November 2026

Student-approved trial; no new card designs, bot strategy, package or scene
layout changes. Project Context is the rule authority. Keep the untracked
`HANDOFF_2026-11-02.md` unchanged as a historical handoff.

## Rules

- Start at 6000 LP. Field Monster KO: Basic 500, Advanced 1000, Supreme 1500,
  Ultimate 2000 LP, independent of Energy. Replaces the old penalty entirely.
- Battle/effect/maximum-HP loss can KO. Tribute is not KO. Existing battle-only
  triggers remain battle-only. Both players losing in one KO batch is a draw;
  a local draw is archived as Draw, not recorded as a win or loss.
- Printed HP stays constant. Maximum HP modifiers preserve damage: 600/900
  becomes 900/1200 after +300 maximum HP. Removing 500 from 300/1500 gives
  0/1000 and KO. Resolve the whole effect/link then KO cleanup before proceeding.
- Recovery is capped, cannot revive, and events report actual recovered HP.
  Legendary Fighter consumes all Energy even if some/all recovery is wasted.
- Tide Sprite gains +300 maximum HP, preserving damage, once per turn per copy.
  It resets when leaving play/recovered to hand, like other field stat modifiers.

## Approved card changes

| Card | Old HP/ATK/DEF | New HP/ATK/DEF |
|---|---|---|
| Dawnwing Fairy | 1000/800/800 | 700/400/150 |
| Gravebound Imp | 1100/900/600 | 800/500/200 |
| Tide Sprite | 1000/600/900 | 900/450/200 |
| Stone Guardian | 1200/700/1100 | 1000/350/450 |
| Fire Dragon | 1800/1200/900 | 1600/1000/300 |
| Sky Sage | 2600/2200/1800 | 2500/1400/600 |
| Legendary Fighter | 3000/1000/2500 | 3500/1000/800 |

Stone gains 300 DEF instead of 500; Guardian Shift gains 500 instead of 1000.
Guardian's lasting duration and conditional position/Energy removal unchanged.
All ranks, attributes, types, artwork, IDs, attack costs and deck copies unchanged.

## Design guidance (not hard validation)

Basic 500–1200 HP; Advanced 1200–2000, typical 1600; Supreme 2000–3000,
typical 2500; Ultimate 3000–4000, typical 3500. Overlap intentional.
Ordinary ATK 50–70% HP, DEF 20–25%; specialists, no-effect beaters and strong
utility effects can deviate. Evaluate turns-to-KO, tribute and Energy costs,
counterplay and lock risk. Stone's stacking DEF is a known balance watchpoint.

## Unity checks

1. Unity Editor, Edit Mode (top-center Play triangle off): wait for script import.
   Project > Assets/Data/Cards/Monsters: select each card and compare stats above.
   If migration has not run, menu Card Game > Sample Cards > Apply November 3
   Balance performs the targeted data update through Unity APIs. Do not run
   broad sample regeneration just to update these cards.
2. Edit Mode > Card Game > Diagnostics > Verify November HP and KO Balance.
   Expect PASS in Console; report is Logs/HealthBalanceVerification.txt.
   Tests use isolated fixtures, not the player's save/profile.
3. Project > Assets/Scenes > AppShell, then top-center Play triangle (Ctrl+P).
   Enter Game > PvE. Both players start with 6000 LP; card details show new stats.
4. Summon Tide Sprite: `HP 900/900`. Activate: `HP 1200/1200`.
   After damage, another legal activation must raise both current and maximum
   by 300, not remove the accumulated damage. Detail panel shows current/max.
5. Legendary Fighter: after a battle KO, all its Energy disappears; HP recovers
   by 500 per removed Energy but not above maximum, including the animation.
6. Basic KO deducts 500 LP even with zero Energy; other ranks deduct 1000/1500/2000.
   Tribute does not deduct LP. Stone gains 300 DEF; Guardian Shift adds 500.
7. Check six Monster HUDs, Defense orientation, attack motion, grave movement
   and Auto pacing. HealthText displays `HP current/maximum`, with its original
   font/size/alignment/position unchanged and no health bar or saved scene/prefab changes.

Do not infer APK/Windows or full Play Mode acceptance from Editor checks.
No commit/push is requested in this slice; checkpoint after student acceptance.

## Verification status

4 November continuation: C# Editor project builds with 0 warnings/errors.
Direct checks against the compiled core passed for damage-preserving maximum-HP
increase/decrease, capped recovery, maximum-HP-loss KO/no implicit revival,
6000 starting LP and all four configured rank penalties. These checks do not
validate Unity assets or full action integration.

Asset migration and Unity verification remain pending. In Unity Editor, Edit
Mode, after script import: Card Game > Diagnostics > Apply and Verify November
HP Balance performs the targeted migration and runs the HP/KO checks plus the
existing battle/tribute/turn/Normal Spell diagnostics. Expect the HP/KO PASS
report in Logs/HealthBalanceVerification.txt and no red Console errors.
This migration saves only its eight card targets and the maximum-HP component;
it does not save unrelated dirty assets or edit scene/prefab layouts.
Play Mode acceptance remains pending. No commit/push has been performed.
