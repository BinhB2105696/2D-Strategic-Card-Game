# HANDOFF â€” Hard H3 / AI preparation checkpoint
Updated 2026-11-17; base commit4618e4a. Read AGENTS.md first.

## Current goal
- Thesis: multi-level bots in a2D tactical card game; Hard target>=75% vs Normal, ML comparison later.
- Current review entry: Docs/AI_BOT_CHECKPOINT.md. Roadmap records history; read relevant sections only.

## Done
- Hard sources: Assets/Scripts/Bots/{HardBot,HardTacticalPolicy,HardStrategicEvaluation,IStateObservingBot}.cs.
- H3 uses bounded single-observer ISMCTS with public hidden-state sampling, Normal guidance, tactical preparation and hand/board portfolio evaluation.
- Search defaults96 iterations/depth6/rollout64/horizon+2; stops at next own Main boundary.
- Exact fork/public accessors were added earlier in Core; Normal exposes its existing score/interface. No Core/Easy/Normal tuning during H1-H3.
- AI schema2:21 global features,18/entity, public retaliation marks; no true hidden hand/deck order/seed in policy inputs.
- Two verified pilot datasets, simulation-only collection decks and Tools/Training encoder/data preparation exist.
- Normal imitation subset1124 meaningful-choice samples:878 train,246 validation; no trainer/model/inference yet.
- Latest experiment: H3 vs Normal, Starter Balance v4, seeds420001-420060,2 swapped seats each:72W/48L=60%.
- Batches23/40,27/40,22/40.120 Finished;0 errors/draws/truncations. Target75% not achieved.
-60 paired seeds:14 double wins,2 double losses(420047,420049),44 splits.
- Hardseat0=32/60,seat1=40/60;first41/60,second31/60. Seat index is not move order.
- Local result: SimulationResults/Experiments/Hard/H3_vs_Normal_StarterBalance_20261116_132958/.
- Git-reviewable summaries and5 standout-pair records: Docs/AI_BOT_EVIDENCE/h3-vs-normal/.
- Prior H3 checks PASS:578 prototype checks plus H1/H2/H3 tactical cases;6 encoder tests previously PASS.

## In progress / next
- User authorized publishing this checkpoint to origin/main; inspect Git history for the resulting commit.
- Historical comparisons: Docs/AI_BOT_HISTORY.md; Claude review prompt: Docs/CLAUDE_HARD_REVIEW_PROMPT.md.
- Historical comparisons: Docs/AI_BOT_HISTORY.md; Claude review prompt: Docs/CLAUDE_HARD_REVIEW_PROMPT.md.
- Current analysis priority: sustainable offensive pressure and preserving/dismantling draw engines.
- Review both-loss seeds alongside double-win guards; do not fix individual seeds with exceptions.
- User launches long simulation batches after assistant prepares them; no further matches run automatically.
- ML next: small imitation trainer/inference design; no installs/train/cloud upload without specific approval.

## Important constraints / portability
- Keep Unity2022.3.62f3/packages. Preserve all uncommitted work; no broad cleanup/refactor.
- Core/game rules/card definitions/Easy/Normal are read-only unless user specifically authorizes a change.
- All actions use actual GameRules; no hidden information or seed-specific policy exceptions.
- Response/trigger search roots currently use explicit Normal fallback; full UI/Player integration is not verified by the standalone benchmark.
- SimulationResults/ and Tools/Simulation/Control/ are ignored: full traces, binaries, data and prepared pointers remain local.
- Run-HardBatch.ps1 requires a prepared frozen package; a clone alone cannot reproduce old batches with it.
- Hard benchmark data is replayVerified=false/trainingEligible=false; published review records are not ML policy inputs.
- State hashes can depend on executable assembly names; preserve matching runtime/source/catalog metadata.
- Candidate commit details are in Docs/AI_BOT_CHECKPOINT.md and Tools/Simulation/Control/commit-checkpoint-plan.json.
- Unrelated TMP font changes, simulation scene generated IDs and old HANDOFF archive are preserved outside the proposed commit.

## Quick verification commands (when needed)
- git diff --check -- Assets/Scripts Docs Tools Assets/Simulation/SimulationCatalog.asset AGENTS.md
- py -3 -B -m unittest discover -s Tools/Training -p test_action_features.py
- .\Tools\Simulation\Run-HardBatch.ps1 -ValidateOnly (local prepared package only; no matches)
- Unity menu: Card Game/Simulation/Verify Hard Bot Prototype (not a win-rate benchmark).
