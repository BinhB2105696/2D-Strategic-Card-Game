"""Run the first consensus-labelled DAgger iteration from frozen mixed v1."""
import argparse
from collections import Counter
import json
from pathlib import Path

import torch
from torch.utils.data import ConcatDataset

from export_learned_policy import dataset_files, parity_fixture, write_policy_artifact
from policy_model import ActionScorer
from train_imitation import JsonlPolicyDataset, limit_dataset, make_loader, read_json, run_epoch, sha256


TRAIN_SEEDS = set(range(2026111977, 2026111981))
VALIDATION_SEEDS = {2026111981, 2026111982}
TRAINING_SEED = 20261120


def records(path):
    with Path(path).open(encoding="utf-8-sig") as stream:
        for line in stream:
            yield json.loads(line)


def collect_labels(receipt, baseline_hash):
    run = Path(receipt["runDirectory"]).resolve()
    report = read_json(receipt["replayReport"])
    meta = read_json(run / "run.json")
    if (receipt["policySha256"] != baseline_hash or meta["learnedPolicySha256"] != baseline_hash
            or (meta["rulesVersion"], meta["catalogVersion"], meta["aiContract"]["schema"]) != (6, 4, 3)
            or not meta["config"]["compareLearnedTeachers"] or report["status"] != "completed"
            or Path(report["sourceRun"]).resolve() != run or report["verifiedMatches"] != 48
            or len(report["results"]) != 48):
        raise ValueError("Expected the verified 48-game mixed v1 collection")

    data = {"train": [], "validation": []}
    coverage, counts = Counter(), Counter()
    seen = set()
    provenance = []
    for match in report["results"]:
        trace = (run / "traces" / match["trace"]).resolve()
        if trace.parent != run / "traces" or trace in seen:
            raise ValueError(f"Invalid or duplicate source trace: {trace}")
        seen.add(trace)
        checked = read_json(match["report"])
        replay = Path(match["dataset"]).resolve()
        if (not checked["verified"] or not checked["completeMatch"] or checked["truncated"]
                or Path(checked["trace"]).resolve() != trace or Path(checked["dataset"]).resolve() != replay):
            raise ValueError(f"Replay did not verify the full source trace: {trace}")
        verified_rows = list(records(replay))
        source = verified_rows[0]
        if (source["record"] != "dataset_start" or source["schema"] != 3
                or Path(source["sourceTrace"]).resolve() != trace
                or source["sourceFingerprint"] != meta["sourceFingerprint"]
                or source["catalogFingerprint"] != meta["catalogFingerprint"]):
            raise ValueError(f"Invalid replay source metadata: {replay}")
        decisions = {row["sequence"]: row for row in verified_rows if row["record"] == "transition"}
        if len(decisions) != checked["verifiedDecisions"]:
            raise ValueError(f"Missing or duplicate replay decisions: {replay}")

        trace_rows = records(trace)
        start = next(trace_rows)
        seed = start["seed"]
        if seed not in TRAIN_SEEDS | VALIDATION_SEEDS or start["matchId"] != source["matchId"]:
            raise ValueError(f"Trace does not belong to the frozen collection: {trace}")
        sides = [i for i in range(2) if start[f"bot{i}"] == "Learned"]
        side, = sides
        opponent = start[f"bot{1 - side}"]
        coverage[seed] += 1
        split = "validation" if seed in VALIDATION_SEEDS else "train"
        for row in trace_rows:
            if row["record"] != "attempt" or row["automatic"] or row["action"]["player"] != side:
                continue
            verified = decisions[row["sequence"]]
            if (verified["player"] != side or verified["policyInputJson"] != row["policyInputJson"]
                    or verified["selectedActionIndex"] != row["selectedActionIndex"]):
                raise ValueError(f"Decision not matched to replay: {trace}:{row['sequence']}")
            policy = json.loads(verified["policyInputJson"])
            mask = policy["legalActionMask"]
            candidates = len(policy["legalActions"])
            eligible = (candidates >= 2 and row["turn"] <= 8 and row["phase"] in ("Main", "Battle")
                        and policy["globalFeatures"][2:6] == [1, 1, 0, 0])
            comparison = row.get("teacherComparison")
            if bool(comparison) != eligible:
                raise ValueError(f"Missing or unexpected teacher comparison: {trace}:{row['sequence']}")
            if not eligible:
                continue
            learner, normal, hard = row["selectedActionIndex"], comparison["normalIndex"], comparison["hardIndex"]
            if mask != [1] * candidates or any(type(i) is not int or not 0 <= i < candidates for i in (learner, normal, hard)):
                raise ValueError(f"Invalid policy candidate index: {trace}:{row['sequence']}")
            counts["compared"] += 1
            if comparison["hardFallback"] or comparison["hardIterations"] <= 0:
                counts["hard_skipped"] += 1
                continue
            if normal != hard:
                counts["teachers_disagree"] += 1
                continue
            counts["learner_differs"] += learner != hard
            data[split].append({
                "policy": policy, "selected_action_index": hard, "learner_index": learner,
                "normal_index": normal, "hard_index": hard, "hard_iterations": comparison["hardIterations"],
                "group_seed": seed, "episode": start["matchId"], "sequence": row["sequence"],
                "opponent": opponent, "phase": row["phase"], "turn": row["turn"],
            })
        provenance.append({"trace": str(trace), "trace_sha256": sha256(trace), "replay_sha256": sha256(replay),
                           "report": str(Path(match["report"]).resolve())})
    if set(coverage) != TRAIN_SEEDS | VALIDATION_SEEDS or set(coverage.values()) != {8}:
        raise ValueError("Each frozen seed must have eight trajectories")
    if any(not data[split] for split in data):
        raise ValueError("Consensus labels are required in both splits")
    return data, dict(counts), provenance


def train(collection, selection_path, output):
    if output.exists():
        raise ValueError("DAgger output already exists")
    selection = read_json(selection_path)
    frozen, = [item for item in selection["selected"] if item["dataset"] == "mixed_hard_normal"]
    base = dataset_files(frozen, "feature_spec.json", "train.jsonl", "validation.jsonl")
    frozen_artifact = selection_path.parent / "learned-policy-v1"
    artifact = read_json(frozen_artifact / "export.json")
    checkpoint_path = Path(frozen["checkpoint"])
    if sha256(checkpoint_path) != artifact["checkpointSha256"]:
        raise ValueError("Frozen mixed checkpoint checksum changed")
    spec = read_json(base / "feature_spec.json")
    saved = torch.load(checkpoint_path, weights_only=True, map_location="cpu")
    if saved["feature_spec"] != spec or saved["epoch"] != frozen["bestEpoch"]:
        raise ValueError("Frozen mixed checkpoint metadata changed")
    original_manifest = read_json(base / "training_manifest.json")
    original_seeds = set(original_manifest["train_seeds"] + original_manifest["validation_seeds"] + original_manifest["test_seeds"])
    if original_seeds & (TRAIN_SEEDS | VALIDATION_SEEDS):
        raise ValueError("DAgger seeds overlap original data")
    data, counts, provenance = collect_labels(read_json(collection), artifact["artifactSha256"])
    output.mkdir(parents=True)
    for split, samples in data.items():
        (output / f"{split}.jsonl").write_text("".join(json.dumps(s) + "\n" for s in samples), encoding="utf-8")
    manifest = {"counts": {split: len(samples) for split, samples in data.items()}, "labels": counts,
                "train_seeds": sorted(TRAIN_SEEDS), "validation_seeds": sorted(VALIDATION_SEEDS),
                "baseline_checkpoint_sha256": artifact["checkpointSha256"], "sources": provenance,
                "collection_receipt": str(collection.resolve())}
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    torch.manual_seed(TRAINING_SEED)
    torch.set_num_threads(4)
    torch.use_deterministic_algorithms(True)
    device = torch.device("cpu")
    model = ActionScorer(spec["context_size"], spec["action_size"], saved["config"]["hidden_sizes"])
    model.load_state_dict(saved["model_state"])
    optimizer = torch.optim.AdamW(model.parameters(), lr=1e-4, weight_decay=1e-4)
    original_train = JsonlPolicyDataset(base / "train.jsonl", spec)
    dagger_train = JsonlPolicyDataset(output / "train.jsonl", spec)
    old_val = make_loader(JsonlPolicyDataset(base / "validation.jsonl", spec), 128, False, TRAINING_SEED, 0)
    new_val = make_loader(JsonlPolicyDataset(output / "validation.jsonl", spec), 128, False, TRAINING_SEED, 0)
    baseline = {"original_validation": run_epoch(model, old_val, device),
                "dagger_validation": run_epoch(model, new_val, device)}
    best_loss, best_epoch = float("inf"), None
    metrics = []
    for epoch in range(1, 6):
        original_subset = limit_dataset(original_train, 3 * len(dagger_train), TRAINING_SEED + epoch)
        train_loader = make_loader(ConcatDataset([original_subset, dagger_train]), 128, True, TRAINING_SEED + epoch, 0)
        metrics.append({"epoch": epoch, "train": run_epoch(model, train_loader, device, optimizer),
                        "dagger_validation": run_epoch(model, new_val, device)})
        loss = metrics[-1]["dagger_validation"]["loss"]
        if loss < best_loss:
            best_loss, best_epoch = loss, epoch
            torch.save(model.state_dict(), output / "best.pt")
        print(json.dumps(metrics[-1]), flush=True)
    model.load_state_dict(torch.load(output / "best.pt", weights_only=True, map_location="cpu"))
    selected = {"epoch": best_epoch, "original_validation": run_epoch(model, old_val, device),
                "dagger_validation": metrics[best_epoch - 1]["dagger_validation"]}
    (output / "metrics.json").write_text(json.dumps({"baseline": baseline, "selected": selected, "epochs": metrics}, indent=2),
                                         encoding="utf-8")
    model_path = write_policy_artifact(model, spec, (6, 4, 3), output)
    fixtures = [parity_fixture(model, spec, f["policy"], f["source"]) for f in read_json(frozen_artifact / "parity.json")]
    (output / "parity.json").write_text(json.dumps(fixtures, allow_nan=False), encoding="utf-8")
    (output / "export.json").write_text(json.dumps({
        "artifactVersion": 1, "artifactSha256": sha256(model_path), "checkpointSha256": sha256(output / "best.pt"),
        "dataset": "dagger_v1", "epoch": best_epoch, "baselineSha256": artifact["artifactSha256"],
        "featureSpec": spec, "validationFixtures": len(fixtures),
    }, indent=2), encoding="utf-8")
    print(json.dumps({"labels": manifest["counts"], "baseline": baseline, "selected": selected}, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("collection", type=Path)
    parser.add_argument("selection_path", type=Path)
    parser.add_argument("output", type=Path)
    train(**vars(parser.parse_args()))
