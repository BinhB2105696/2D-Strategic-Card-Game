# Handoff — Normal approximately 7:3 checkpoint
Updated: 10 November 2026. Unity 2022.3.62f3; Android first, Windows second.
Read root AGENTS.md, then this file only; inspect other sections as needed.

## Current goal
Save the accepted Normal milestone and discuss the next scope; no next task selected.
Student accepts approximately 7:3 vs Easy; 8:2 is optional research, not required now.

## Done
- NormalBot.cs: self-effect/Equip valuation, projected tribute ATK, defense when
  an attack cannot be prepared, ATK/rank target priority, situational Energy cost
  and deterministic tie handling. Normal Auto uses the same policy.
- NormalBotVerification.cs: tactical PASS and 6 automated fixture matches.
- Runner build 0 errors/0 warnings. Updated Normal completed 300/300 matches,
  no errors/truncations, no decision above 1,000 ms; CSV/JSONL/trace counts matched.
- Before: 88/150 wins (58.67%). Same seeds after: 108/150 (72%).
  Unseen seeds: 107/150 (71.33%). Two preset Decks, three pairings, swapped bots.
- Student balance: Tide HP 600/+200 max HP; Stone DEF 300/+100 DEF;
  Sky ATK 1600, attack Energy 2, pay 2 Energy to draw 2.
- Removed legacy sample generation/balance and automatic rewrite paths in
  CardSampleTool, bootstrap and HP verification; artwork/validation preserved.
- SimulationBuildTool now compiles and waits for assembly reload before building.
- README, PROJECT_CONTEXT and simulation notes reflect the current milestone.
- Prior: offline APK 0.1.0, guest profile/decks/settings/history, Duel Log,
  Easy/Normal modes and compact HP bar. Physical-device acceptance pending.

## Decisions / limits
- Freeze Easy/Normal as references; Hard/ML optional and measured fairly.
- Core/rules/Easy unchanged by this bot update; preserve unrelated changes.
- No claim of full all-effect acceptance or strength on arbitrary decks.
- New Normal/Auto Play Mode acceptance pending. Existing APK predates this update;
  rebuild before Android testing of current card balance/Normal.
- Full state hashes/verified replay, PvP/accounts, playable Windows build open.
- Card expansion, effects/chain review and Duel Log polish await a selected scope.
- No automatic batch resume, dataset storage limit or ML adapter yet.
- Feature freeze target 25 November; defense expected early December.

## Artifacts / next
- SimulationResults/Experiments/20261110_NormalEasy_Baseline150/ preserves the
  pre-improvement experiment, including raw copies, combined tables and SHA-256.
- Updated same-seed run: SimulationResults/20261110_042425_7622edfb/
- Updated unseen-seed run: SimulationResults/20261110_042629_d98dd7ff/
- Raw results/Control/Build outputs remain ignored local files on D, not in Git.
- Runtime fingerprint: e2a5c1332e2fb8d90cd292d03bcca2d6d407bd2d799b3c9526d21346e1137a1a
- Tools/Simulation/TESTING_NOTES.md has counts, group results, limits and provenance.
- Discuss replay/state-hash foundation, scoped card expansion, Hard/ML preparation;
  networking/device thesis commitments remain open, not silently removed.

## Quick verification
- Card Game > Diagnostics > Verify Normal Bot; Play Mode Normal and Auto.
- Card Game > Simulation > Build Headless Windows Runner.
- powershell -NoProfile -File .\Tools\Simulation\Run-Simulation.ps1 -Config .\Tools\Simulation\normal-vs-easy-evaluation.json
- Use normal-vs-easy-holdout.json for seeds 1026–1050; both sets have 150 matches.
- Do not repeat passed checks without a change/new reason. No commit/push unless asked.
