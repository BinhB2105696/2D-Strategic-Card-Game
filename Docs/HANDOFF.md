# Handoff — Headless runner acceptance checkpoint
Updated: 10 November 2026. Unity 2022.3.62f3; Android first, Windows second.
Read root AGENTS.md, then this file only; consult other sections as needed.

## Current goal
Checkpoint accepted runner tests and discuss the next major project milestone.
No next implementation selected yet.

## Done
- Offline demo 0.1.0: APK build/signature verified; guest profile, decks,
  settings and match archives persist locally. Physical-device acceptance pending.
- Easy/Normal Duel mode and matching Auto work; current-HP health bar accepted.
- Basic in-match Settings > Duel Log accepted; detailed polish deferred.
- 0027715: headless Windows runner, same GameSession/GameRules, isolated scene,
  seed/deck metadata, CSV/JSONL, sampled traces and paired summary statistics.
- Original 30-match batch finished; post-review 6/6 action/result digests matched.
- 4f468e2: Test-Runner.ps1, Editor build diagnostics and updated testing notes.
- Acceptance 10/10: turn/time limits, trace off, five invalid configurations,
  interrupted batch and readonly progress-file failure. Three acknowledged
  matches survived interruption; write failure exited nonzero, destination intact.
- Latest runner build succeeded with 0 errors/0 warnings. Previous warning was
  building while changed Editor code had not finished compiling.
- No Core/rule/bot/UI changes in this acceptance slice. Runtime fingerprint:
  c381754b9d7ade75aabfd188f5cce7e848c4cb12bbdcedab31ac70bfb00c9e46

## Next / pending
- Choose next scope: measured Easy/Normal baseline and tuning, then verified
  deterministic replay/state hashes; discuss networking feasibility promptly.
- PvP/account flow is unimplemented; the menu reports coming soon.
- Device acceptance, playable Windows build and thesis experiments remain open.
- README roadmap/remaining work and PROJECT_CONTEXT sections 12/15 still describe
  building the headless runner as future work; it now exists and passed checks.
- Card expansion, detailed effects/chain review and Duel Log polish stay deferred.
- No automatic batch resume, between-match checkpoint recovery UI or ML adapter.
- Disk-full and result-stream mid-write faults have not been tested.

## Decisions / limits
- Do not change Core/rules/unrelated files without discussing essential changes.
- Action/result digests are not complete internal-state hashes or verified replay.
- swapBots swaps policies, keeping Decks in Player slots; not an independent
  first-player or Deck-swap control. Design evaluation comparisons accordingly.
- Default detailed traces cover early Easy/Easy jobs; sample Normal separately.
- Keep generated outputs on D: SimulationResults and Tools/Simulation/Control.
- No Google login/cloud sync. Normal is heuristic, not trained ML.
- PvP, replay and platform verification outrank optional Hard Bot/ML in thesis scope.
- Feature freeze target 25 November; defense expected early December.
- Preserve unrelated font edits, root AGENTS.md and dated older handoff.

## Quick verification / artifacts
- Tools/Simulation/TESTING_NOTES.md records observed passes and remaining limits.
- powershell -NoProfile -File .\Tools\Simulation\Test-Runner.ps1
- Acceptance report: Tools/Simulation/Control/Acceptance/20261110_061854/report.json
- Build diagnostics: Tools/Simulation/Control/latest-build-report.json
- Build runner: Card Game > Simulation > Build Headless Windows Runner.
- Do not repeat passed tests without changes or a new reason.
