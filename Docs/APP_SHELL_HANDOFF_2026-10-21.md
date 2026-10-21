# App shell handoff — 21 October 2026

PROJECT_CONTEXT.md remains the authority for rules and thesis deadlines.
The current operational guide is APP_FOUNDATION_GUIDE_2026-10-21.md (Vietnamese).

## Current approved slice

- Author AppShell views in Edit Mode using Unity Editor APIs. Generate once;
  preserve manual layout edits. Runtime binds references and fills dynamic lists.
- Home → Profile (avatar/name/history), Settings (master/music/SFX preferences),
  Deck Builder, selected saved deck → Easy Bot duel → result/history → Home.
- A one-use AppMatchLaunch request carries the full validated 30–40-card deck.
  Direct Play of SampleScene remains the fixed demo. Small adapters in
  DuelBoardReadOnlyDemo and DuelHudPrototypeController connect app startup/exit;
  no core rules or board layout changes are authorized by this slice.
- Device-local schema v2 migrates the previous v1 file; stable local player/deck
  IDs, settings, favorites, decks and recent match summaries. Accepted actions,
  deck IDs, seed and version metadata are archived for later replay work.
- Local ID is not online authentication. No password backend, cloud account,
  network SDK, PvP implementation, or trusted online win rate is provided.
- Auto/hot-seat practice and abandoned matches are explicitly labeled and excluded
  from the manual-PvE win rate. This default can be discussed after user testing.

## Safe late-October work

1. Follow the exact Edit Mode preview and Play tests in the Vietnamese guide.
2. Tune AppShell scene children and AppDuelOverlay prefab visually. Dynamic card
   contents are previews; adjust the grid and shared card scale, not each preview.
3. Preserve serialized references. Never hand-write scene/prefab/asset/meta YAML.
   Do not regenerate or delete the UI simply to move a button or change a font.
4. Keep company/product/save filename unchanged. Do not overwrite an invalid
   profile automatically or treat a renamed guest as a new authenticated account.
5. Do not edit core/card effects/IDs/font assets/packages for UI polish. The two
   Cinzel TMP font assets already had user changes before this work; preserve them.
6. Unity menus under Card Game → App Shell prepare views, verify foundations and
   build development Windows/APK outputs. Compilation is not Play/device evidence.
   Read Console and Logs/AppFoundationVerification.txt before claiming success.
7. Obtain the Android development build and check portrait/touch/persistence before
   spending more days on polish. Keep early-November Normal Bot, simulation/replay
   and online feasibility milestones; 25 November feature freeze is unchanged.

## Checkpoint handoff

After Unity imports the scripts, include generated scene/prefab/.meta files along
with app scripts, the two narrow duel adapters and documentation. Check the diff
for unrelated files; do not automatically commit the pre-existing TMP font edits.
Wait for the student's Play Mode acceptance before the requested checkpoint push.
