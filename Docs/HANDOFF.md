# Handoff — Replay foundation and verified PvP SDK checkpoint
Updated: 10 November 2026. Unity 2022.3.62f3; Android first, Windows second.
Read root AGENTS.md, then this file only; inspect other sections as needed.

## Current goal
Checkpoint replay/hash/AI-contract MVP and compatible PvP SDK installation.
User authorized updating docs, committing and pushing this checkpoint.
Next: discuss room-code PvP MVP design; no matchmaking. Hard/ML deferred.

## Done — replay foundation
- New SimulationStateHash.cs, SimulationAiContract.cs, SimulationReplayFormat.cs
  and SimulationReplay.cs; SimulationEngine/Runner integration uses schema 2.
- New ReplayFoundationVerification.cs, Replay-Simulation.ps1, Test-Replay.ps1.
- Read-only private pure-state hashes, replay validation, masked AI contract,
  legal candidates/masks, per-player transitions and terminal rewards.
- Pure verification PASS; Windows runner build 0 errors/0 warnings.
- Native acceptance 18/18 PASS: 6 matches, 1,222 actions, 922 decisions;
  11 corrupted/version/legacy traces rejected, 1 limited prefix verified.
- README, PROJECT_CONTEXT, simulation README/TESTING_NOTES updated.
- Core/rules/Easy/Normal unchanged. No actual training or network protocol.

## Done — PvP SDK installation
- NGO 1.15.1, Relay 1.2.0, Authentication 3.6.0, Transport 1.5.0;
  Core 1.15.1, QoS 1.3.2, Mono Cecil 1.11.6, Newtonsoft JSON 3.2.1.
- Preserved/pinned Collections 1.2.4, Burst 1.8.21, Mathematics 1.2.6.
- PvpSdkVerification.cs PASS 2026-11-10T07:30:48Z; student confirmed
  Console 0 errors/0 warnings. Original 54 packages and Editor unchanged.
- Expired Unity login blocked downloads; re-login resolved it. Diagnostic
  PackageInfo collision fixed with a type alias. Safe Mode exited successfully.
- Manifest, resolved package lock, verification plan/report checkpointed.
- MPS unified SDK not installed: evaluated versions need Collections 2.2.1.
  Standalone Relay is legacy; review service support before production release.

## Next / constraints
- Design create room -> share code -> join -> 1v1, then service configuration
  and two-device Android/Windows connectivity. No services configured yet.
- Never upgrade Unity 2022.3.62f3 or change Core/rules without explicit approval.
- SDK PASS is installation/API verification, not an online match/device test.
- Define host authority, private views, lifecycle/disconnects and version checks;
  simulator private hashes/decks/seeds are not a network sync protocol.
- Online recovery/history/results unimplemented; preserve local guest/PvE data.
- Hard/ML workflow saved in PROJECT_CONTEXT section 12 until cards/rules stable.
- Replay MVP tested Windows/Mono only; schema-1 baselines remain untouched.
- Existing APK 0.1.0 predates later balance/Normal updates; device checks pending.
- Preserve unrelated font/AGENTS/dated-handoff edits. No further commit unasked.

## Evidence / quick checks
- Card Game > Networking > Verify Pinned PvP SDKs.
- SDK plan/status: Tools/Simulation/Control/PvpSdk/{plan,status}.json.
- Card Game > Simulation > Verify Replay State and AI Contract.
- Replay report: Tools/Simulation/Control/ReplayAcceptance/20261110_132014_b683aa0f/report.json
- Source runs: SimulationResults/20261110_062015_7ab0ec9e and 20261110_062116_6c5a775d.
- Replay output: <run>/replay/<UTC>_<id>/; all generated runs stay on D.
- Do not repeat passed tests without changes/new reason.
- Prior pushed checkpoint bd5d71d: Normal 108/150; unseen seeds 107/150.
