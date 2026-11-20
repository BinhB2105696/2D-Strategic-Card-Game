"""Fine-tune frozen mixed v1 using stable preferences from paired rollouts."""
import argparse
from collections import Counter
from itertools import combinations
import json
import math
from pathlib import Path
from statistics import mean

import torch
from torch.utils.data import ConcatDataset

from action_features import encode
from export_learned_policy import dataset_files, parity_fixture, write_policy_artifact
from policy_model import ActionScorer
from train_imitation import JsonlPolicyDataset, limit_dataset, make_loader, read_json, run_epoch, sha256


TRAIN_SEEDS = set(range(2026112081, 2026112090))
VALIDATION_SEEDS = {2026112090, 2026112091, 2026112092}
TRAINING_SEED = 20261121
MARGIN = 0.02
LEARNER_SHARDS = (("learner-v1", 2026112141), ("learner-v1-extension-1", 2026112161),
                  ("learner-v1-extension-2", 2026112173))


def records(path):
    with Path(path).open(encoding="utf-8-sig") as stream:
        for line in stream:
            yield json.loads(line)


def stable_preferences(indices, scores):
    for i, j in combinations(range(len(indices)), 2):
        delta = [a - b for a, b in zip(scores[i], scores[j])]
        first, second = mean(delta[:4]), mean(delta[4:])
        if first * second > 0 and abs(mean(delta)) >= MARGIN:
            yield (indices[i], indices[j]) if first > 0 else (indices[j], indices[i])


def collect_preferences(receipt, spec, first_seed=None, policy_hash=None):
    learner_mode = first_seed is not None
    train_seeds = set(range(first_seed, first_seed + 9)) if learner_mode else TRAIN_SEEDS
    validation_seeds = set(range(first_seed + 9, first_seed + 12)) if learner_mode else VALIDATION_SEEDS
    bot = "Learned" if learner_mode else "HardLow"
    flag = "compareLearnedRollouts" if learner_mode else "collectPairedRolloutScores"
    label_field = "learnerRolloutComparison" if learner_mode else "pairedRollout"
    run = Path(receipt["runDirectory"]).resolve()
    report = read_json(receipt["replayReport"])
    meta = read_json(run / "run.json")
    if ((meta["rulesVersion"], meta["catalogVersion"], meta["aiContract"]["schema"]) != (6, 4, 3)
            or not meta["config"][flag] or report["status"] != "completed"
            or (learner_mode and meta["learnedPolicySha256"] != policy_hash)
            or Path(report["sourceRun"]).resolve() != run or report["verifiedMatches"] != 48
            or len(report["results"]) != 48):
        raise ValueError("Expected the replay-verified 48-game rollout collection")

    data = {"train": [], "validation": []}
    validation_choices = {"hard_favored": [], "learner_favored": []}
    counts = {split: Counter() for split in data}
    coverage, provenance, seen = Counter(), [], set()
    for match in report["results"]:
        trace = (run / "traces" / match["trace"]).resolve()
        if trace.parent != run / "traces" or trace in seen:
            raise ValueError(f"Invalid or duplicate source trace: {trace}")
        seen.add(trace)
        checked = read_json(match["report"])
        replay_path = Path(match["dataset"]).resolve()
        if (not checked["verified"] or not checked["completeMatch"] or checked["truncated"]
                or Path(checked["trace"]).resolve() != trace or Path(checked["dataset"]).resolve() != replay_path):
            raise ValueError(f"Replay did not verify the full trace: {trace}")
        replay = records(replay_path)
        source = next(replay)
        if (source["record"] != "dataset_start" or source["schema"] != 3
                or Path(source["sourceTrace"]).resolve() != trace
                or source["sourceFingerprint"] != meta["sourceFingerprint"]
                or source["catalogFingerprint"] != meta["catalogFingerprint"]):
            raise ValueError(f"Incompatible replay results: {replay_path}")
        decisions = {row["sequence"]: row for row in replay if row["record"] == "transition"}
        if len(decisions) != checked["verifiedDecisions"]:
            raise ValueError(f"Incomplete replay decisions: {replay_path}")
        rows = records(trace)
        start = next(rows)
        seed = start["seed"]
        if (seed not in train_seeds | validation_seeds or start["matchId"] != source["matchId"]
                or sorted((start["bot0"], start["bot1"])) != [bot, "Normal"]):
            raise ValueError(f"Trace does not belong to the frozen collection: {trace}")
        coverage[seed] += 1
        side = 0 if start["bot0"] == bot else 1
        split = "validation" if seed in validation_seeds else "train"
        labelled_turns = set()
        for row in rows:
            label = row.get(label_field)
            if label is None:
                continue
            decision = decisions[row["sequence"]]
            turn_phase = (row["turn"], row["phase"])
            if (row["record"] != "attempt" or row["automatic"] or row["action"]["player"] != side
                    or decision["player"] != side or decision["policyInputJson"] != row["policyInputJson"]
                    or decision["selectedActionIndex"] != row["selectedActionIndex"]
                    or turn_phase in labelled_turns or row["turn"] > 8 or row["phase"] not in ("Main", "Battle")):
                raise ValueError(f"Score label does not match a verified decision: {trace}:{row['sequence']}")
            labelled_turns.add(turn_phase)
            policy = json.loads(decision["policyInputJson"])
            if policy["globalFeatures"][2:6] != [1, 1, 0, 0]:
                raise ValueError(f"Rollout label requires an open decision: {trace}:{row['sequence']}")
            if learner_mode:
                counts[split]["comparisons"] += 1
                if label["status"] != "scored":
                    if label["relativeScores"] is not None:
                        raise ValueError(f"Unexpected scores on a skipped decision: {trace}:{row['sequence']}")
                    continue
            context, actions = encode(policy, spec)
            indices, scores = label["legalIndices"], label["relativeScores"]
            if (not 2 <= len(indices) <= 4 or len(scores) != len(indices) or len(set(indices)) != len(indices)
                    or any(type(i) is not int or not 0 <= i < len(actions) for i in indices)
                    or any(len(s) != 8 or any(not math.isfinite(v) or abs(v) > 1 for v in s) for s in scores)
                    or scores[0] != [0.0] * 8):
                raise ValueError(f"Malformed paired scores: {trace}:{row['sequence']}")
            counts[split]["states"] += 1
            if learner_mode:
                learner_index = row["selectedActionIndex"]
                hard_index = label["hardLowIndex"]
                if indices[0] != learner_index or set(indices) != {learner_index, hard_index, label["tacticalIndex"]}:
                    raise ValueError(f"Invalid compared actions: {trace}:{row['sequence']}")
                if hard_index == learner_index:
                    continue
                learner_position, hard_position = indices.index(learner_index), indices.index(hard_index)
                indices = [learner_index, hard_index]
                scores = [scores[learner_position], scores[hard_position]]
            counts[split]["candidate_pairs"] += math.comb(len(indices), 2)
            for better, worse in stable_preferences(indices, scores):
                sample = (context, [actions[better], actions[worse]], 0)
                data[split].append(sample)
                counts[split][row["phase"]] += 1
                if learner_mode:
                    direction = "hard_favored" if better == hard_index else "learner_favored"
                    counts[split][direction] += 1
                    if split == "validation":
                        validation_choices[direction].append(sample)
        provenance.append({"trace": str(trace), "trace_sha256": sha256(trace),
                           "replay_sha256": sha256(replay_path), "replay": str(replay_path)})
    if set(coverage) != train_seeds | validation_seeds or set(coverage.values()) != {4}:
        raise ValueError("Every frozen seed must have four verified trajectories")
    if sum(c["states"] for c in counts.values()) != receipt["labelledDecisions"] or not all(data.values()):
        raise ValueError("Missing verified states or stable preferences")
    if learner_mode and sum(c["comparisons"] for c in counts.values()) != receipt["comparedDecisions"]:
        raise ValueError("Learner comparison count differs from receipt")
    identity = (meta["sourceFingerprint"], meta["catalogFingerprint"])
    return data, counts, provenance, validation_choices, identity


def train(collection, selection_path, output, learner_states=False):
    if output.exists():
        raise ValueError("Rollout pilot output already exists")
    selection = read_json(selection_path)
    frozen, = [item for item in selection["selected"] if item["dataset"] == "mixed_hard_normal"]
    base = dataset_files(frozen, "feature_spec.json", "train.jsonl", "validation.jsonl")
    frozen_artifact = selection_path.parent / "learned-policy-v1"
    export = read_json(frozen_artifact / "export.json")
    checkpoint = Path(frozen["checkpoint"])
    if sha256(checkpoint) != export["checkpointSha256"]:
        raise ValueError("Frozen checkpoint checksum changed")
    spec = read_json(base / "feature_spec.json")
    saved = torch.load(checkpoint, weights_only=True, map_location="cpu")
    if saved["feature_spec"] != spec or saved["epoch"] != frozen["bestEpoch"]:
        raise ValueError("Frozen checkpoint metadata changed")
    original = read_json(base / "training_manifest.json")
    old_seeds = set(original["train_seeds"] + original["validation_seeds"] + original["test_seeds"])
    train_seeds, validation_seeds = TRAIN_SEEDS, VALIDATION_SEEDS
    if learner_states:
        train_seeds = {seed for _, start in LEARNER_SHARDS for seed in range(start, start + 9)}
        validation_seeds = {seed for _, start in LEARNER_SHARDS for seed in range(start + 9, start + 12)}
    if old_seeds & (train_seeds | validation_seeds):
        raise ValueError("Rollout seeds overlap original training data")
    receipts = [(collection, None)]
    if learner_states:
        receipts = [(collection / folder / "verified-collection.json", seed) for folder, seed in LEARNER_SHARDS]
    data = {"train": [], "validation": []}
    counts = {split: Counter() for split in data}
    validation_choices = {"hard_favored": [], "learner_favored": []}
    provenance, fingerprints = [], set()
    for receipt, seed in receipts:
        shard, diagnostics, sources, choices, identity = collect_preferences(
            read_json(receipt), spec, seed, export["artifactSha256"])
        fingerprints.add(identity)
        for split in data:
            data[split].extend(shard[split])
            counts[split].update(diagnostics[split])
        for direction in validation_choices:
            validation_choices[direction].extend(choices[direction])
        provenance.extend(sources)
    if len(fingerprints) != 1:
        raise ValueError("Incompatible rollout collection fingerprints")
    output.mkdir(parents=True)
    (output / "manifest.json").write_text(json.dumps({
        "counts": counts, "pair_counts": {split: len(rows) for split, rows in data.items()},
        "margin": MARGIN, "train_seeds": sorted(train_seeds), "validation_seeds": sorted(validation_seeds),
        "baseline_checkpoint_sha256": export["checkpointSha256"], "source_receipt": str(collection.resolve()),
        "sources": provenance,
    }, indent=2), encoding="utf-8")

    torch.manual_seed(TRAINING_SEED)
    torch.set_num_threads(4)
    torch.use_deterministic_algorithms(True)
    device = torch.device("cpu")
    model = ActionScorer(spec["context_size"], spec["action_size"], saved["config"]["hidden_sizes"])
    model.load_state_dict(saved["model_state"])
    learning_rate = 3e-4 if learner_states else 1e-4
    optimizer = torch.optim.AdamW(model.parameters(), lr=learning_rate, weight_decay=1e-4)
    baseline_train = JsonlPolicyDataset(base / "train.jsonl", spec)
    baseline_val = make_loader(JsonlPolicyDataset(base / "validation.jsonl", spec), 128, False, TRAINING_SEED, 0)
    rollout_val = make_loader(data["validation"], 128, False, TRAINING_SEED, 0)
    baseline = {"original_validation": run_epoch(model, baseline_val, device),
                "pair_validation": run_epoch(model, rollout_val, device)}
    epochs = []
    best_loss, best_epoch = float("inf"), None
    epoch_count, batch_size = (12, 64) if learner_states else (6, 128)
    for epoch in range(1, epoch_count + 1):
        original_subset = limit_dataset(baseline_train, 3 * len(data["train"]), TRAINING_SEED + epoch)
        loader = make_loader(ConcatDataset([original_subset, data["train"]]), batch_size, True, TRAINING_SEED + epoch, 0)
        train_metrics = run_epoch(model, loader, device, optimizer)
        validation_metrics = run_epoch(model, rollout_val, device)
        epochs.append({"epoch": epoch, "train": train_metrics, "pair_validation": validation_metrics})
        if validation_metrics["loss"] < best_loss:
            best_loss, best_epoch = validation_metrics["loss"], epoch
            torch.save(model.state_dict(), output / "best.pt")
        print(json.dumps(epochs[-1]), flush=True)
    model.load_state_dict(torch.load(output / "best.pt", weights_only=True, map_location="cpu"))
    selected = {"epoch": best_epoch, "original_validation": run_epoch(model, baseline_val, device),
                "pair_validation": epochs[best_epoch - 1]["pair_validation"]}
    if learner_states:
        selected["validation_directions"] = {
            direction: run_epoch(model, make_loader(rows, 128, False, TRAINING_SEED, 0), device)
            for direction, rows in validation_choices.items()
        }
    (output / "metrics.json").write_text(json.dumps({"baseline": baseline, "selected": selected, "epochs": epochs}, indent=2),
                                         encoding="utf-8")
    model_path = write_policy_artifact(model, spec, (6, 4, 3), output)
    fixtures = [parity_fixture(model, spec, f["policy"], f["source"]) for f in read_json(frozen_artifact / "parity.json")]
    (output / "parity.json").write_text(json.dumps(fixtures, allow_nan=False), encoding="utf-8")
    (output / "export.json").write_text(json.dumps({
        "artifactVersion": 1, "artifactSha256": sha256(model_path), "checkpointSha256": sha256(output / "best.pt"),
        "dataset": "learner_rollout_v1" if learner_states else "paired_rollout_v1",
        "epoch": best_epoch, "baselineSha256": export["artifactSha256"],
        "featureSpec": spec,
    }, indent=2), encoding="utf-8")
    print(json.dumps({"baseline": baseline, "selected": selected}, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("collection", type=Path)
    parser.add_argument("selection_path", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--learner-states", action="store_true", help="Read the three learner collections under the given root.")
    train(**vars(parser.parse_args()))
