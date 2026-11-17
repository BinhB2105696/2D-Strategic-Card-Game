# Hard experiment history — checkpoint 2026-11-17

Read [the current checkpoint](AI_BOT_CHECKPOINT.md) first. This history supports review of improvements and regressions; it is not a new experiment.

## Comparable development trials against Easy

All full H0/H1/H2 trials use Starter Balance v4 in both seats, seeds420001–420060, with two legs swapping bots. Rules/catalog/config/deck match across these versions.

| Version | Hard wins | Losses | Scope |
| --- | --- | --- | --- |
| H0 | 99/120 (82.5%) | 21 | Initial search prototype |
| H1 | 89/120 (74.17%) | 31 | Tactical defense/tribute restrictions |
| H2 | 101/120 (84.17%) | 19 | Shared Energy preparation, softer risks, Normal tie preference |
| H3 | 3/5 selected cases | 2 | Forecast fix and hand/board portfolio; not a full Easy benchmark |

The separate full **H3 vs Normal** trial is72/120 (60%). Do not compare that percentage directly to Easy percentages as if only the Hard version changed.

## What each version changed

- **H0:** bounded information-set search with Normal guidance/opponent rollouts. It could invest Energy into a future tribute, waste allocations, or miss tactical preparation.
- **H1:** added posture preferences, investment/tribute guards and support preservation. It removed some waste but restricted joint plans, sometimes ended Main after preparation, and changed attack tie ordering. Net result was a regression.
- **H2:** shared attack Energy commitments, staged preparation before leaving Main, more permissive risky summons, explicit printed-cost handling, temporary-buff feasibility and restored Normal tie preference. This changed tactical guidance; it did not establish a deeper full-turn search.
- **H3:** fixed prospective passive queries for hand candidates; added a common hand/board portfolio estimate at candidate ranking and leaf evaluation, with feasible tribute packages and discounted repeatable draw. It retains the bounded search and response/trigger fallback limits.

Use [recorded version profiles](AI_BOT_EVIDENCE/history/version-profiles.json) for exact parameters. The repository contains current H3 source; full historical source snapshots/executables are not published in this compact evidence pack.

## Matched outcome changes

| Comparison | Win → loss | Loss → win | Other |
| --- | --- | --- | --- |
| H0 → H1 | 12 | 2 | 87 both win,19 both lose |
| H0 → H2 | 5 | 7 | 94 both win,14 both lose |
| H1 → H2 | 1 | 13 | 88 both win,18 both lose |
| H2 → H3 selected cases | 0 | 3 | 2 remain losses; no general-rate inference |

[All120 seed/seat comparisons](AI_BOT_EVIDENCE/history/seed-comparison.json) contain results, final LP and turns for every H0/H1/H2 match. H3 is null wherever it was not run. A missing cell is not a loss.

## Seven selected historical cases

The files contain start-of-Main snapshots when available, named actions and final results. They omit opponent actions, automatic transitions and routine phase/pass actions; they are not complete replay logs.

| Seed / Hard seat | Why selected | Evidence |
| --- | --- | --- |
| 420033 /1 | H0 win → H2 loss → H3 win; compare Sky draw engine and deployment | [case](AI_BOT_EVIDENCE/history/case-420033-seat1.json) |
| 420038 /1 | H0 win → H2 loss → H3 win; midgame survival/rebuilding | [case](AI_BOT_EVIDENCE/history/case-420038-seat1.json) |
| 420048 /1 | H3 fixes Squire summon posture but still loses; tactical coordination remains unresolved | [case](AI_BOT_EVIDENCE/history/case-420048-seat1.json) |
| 420057 /0 | H3 stages two Basic materials into Sky Sage and recovers a loss | [case](AI_BOT_EVIDENCE/history/case-420057-seat0.json) |
| 420059 /1 | Still loses with H3; board survival and target/preparation sequence need review | [case](AI_BOT_EVIDENCE/history/case-420059-seat1.json) |
| 420005 /0 | H0 loss → H2 win; positive counterexample to regression-only review | [case](AI_BOT_EVIDENCE/history/case-420005-seat0.json) |
| 420050 /1 | H0 loss in45 turns → H2 win in12; pronounced positive change | [case](AI_BOT_EVIDENCE/history/case-420050-seat1.json) |

Current H3-vs-Normal double losses420047/420049 and double-win/split guards are linked from the current checkpoint. Review them alongside these older Easy cases.

## Interpretation and provenance

- Exact action differences are observations. Claims about the sole cause of winning/losing need controlled counterfactual or ablation tests.
- Development seeds were inspected repeatedly. Use reserved/new seeds and other decks for later strength claims.
- Raw state-hash schema1 embeds closed-generic assembly names; differently named runtime executables can have different hashes for equivalent states. Earlier graph-normalized initial-state audits are local evidence; do not compare raw hashes naively.
- Source hashes and export hashes are in [provenance.json](AI_BOT_EVIDENCE/history/provenance.json). Full traces, frozen runtimes and datasets remain local; no replay verification or training eligibility is claimed for this history export.
- No bot/rule/asset changes or new games were performed to publish this evidence.
