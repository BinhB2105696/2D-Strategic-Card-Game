# Handoff — Offline Demo 0.1.0 + Duel Log
Updated: 9 November 2026. Unity 2022.3.62f3; Android first, Windows second.
Read root AGENTS.md, then this file only; inspect other sections when needed.

## Current goal
Save the offline demo checkpoint and choose the next scoped task with the student.

## Done
- Previous checkpoint 977a734: Normal Bot, Easy/Normal Duel mode, matching Auto,
  current-HP HUD/right alignment and rounded five-color health bar.
- HP/KO diagnostic and HUD accepted by student; Normal mode works in Play Mode.
- DuelLogModel/View plus Board/HUD hooks: Settings > Duel Log shows accepted
  actions and ordered events, freezes hidden-info views and recycles scrolling rows.
- DuelLogVerification passed; student confirmed basic in-match operation.
- OfflineDemoBuildTool: build inside the open Editor, validate scratch local saves,
  configure Android, switch platform and create APK without rewriting scene UI.
- APK 0.1.0/code 1: build PASS, 0 errors/0 warnings, signatures verified, 35.52 MiB.
- Android 6+ / API 23 min, API 34 target, IL2CPP ARMv7+ARM64, portrait, no Internet permission.
- Demo package com.binhb2105696.strategiccardgame.demo; Android Debug test signing.
- Local profile/deck/settings/history round-trip, backup/corrupt-file checks passed.
- README, PROJECT_CONTEXT, DUEL_PRESENTATION_PLAN and OFFLINE_DEMO_GUIDE updated.

## Next / pending
- Student device test: install, complete match, return home, restart and check saves.
- Recommend headless batch runner and Easy/Normal comparison CSV before ML training.
- Then deterministic replay/state-hash verification and a safe training adapter.
- Online PvP/account feasibility, Windows build and thesis experiments remain open.
- Card expansion, detailed effect/chain review and Duel Log polish deferred by student.
- No new implementation until the student selects the next task.

## Decisions / limits
- Offline guest profile only; no Google login/cloud sync. No device PASS claimed.
- Duel Log is in-memory for the current match; no persisted event/post-match viewer.
- Normal is heuristic + bounded public-board attack forecast, not trained ML/full-state search.
- Shared BotGameStateView still omits opponent Graveyard details; Normal supplements public data.
- Hard Bot/ML optional; target feature freeze remains 25 November, defense early December.
- Keep demo package/signing key stable for updates. Never commit signing keys/passwords.
- APK/ZIP are ignored local outputs under Build/OfflineDemo; not uploaded to GitHub Releases.
- Preserve unrelated TMP font edits, untracked AGENTS.md and dated older handoff.

## Quick verification / artifacts
- Card Game > Diagnostics > Verify Normal Bot / Verify Duel Log.
- Card Game > App Shell > Build Offline Demo APK (save intended scene edits first).
- APK: Build/OfflineDemo/CardGame-OfflineDemo-0.1.0.apk; ZIP has same base name.
- Docs/OFFLINE_DEMO_GUIDE.md has install/test steps, checksum and rebuild limits.
- git status --short -- Assets/Scripts ProjectSettings README.md Docs