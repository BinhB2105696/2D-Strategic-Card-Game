"""Prepare verified Normal examples and grouped splits, without PyTorch/training."""
import argparse
import hashlib
import json
from contextlib import ExitStack
from pathlib import Path

from action_features import ENCODER_VERSION, encode


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def digest(path):
    result = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def prepare(manifest_paths, output, validation_seeds, test_seeds=None, teachers=None, min_choices=2):
    output = Path(output).resolve()
    if output.exists():
        raise ValueError("Output already exists; choose a new dataset directory")
    manifests = [(Path(path).resolve(), read_json(path)) for path in manifest_paths]
    first = manifests[0][1]
    version = (first["rulesVersion"], first["catalogVersion"], first["aiSchema"],
               first["sourceFingerprint"], first["catalogFingerprint"])
    episodes = []
    runs = {}
    for path, manifest in manifests:
        current = (manifest["rulesVersion"], manifest["catalogVersion"], manifest["aiSchema"],
                   manifest["sourceFingerprint"], manifest["catalogFingerprint"])
        if current != version or read_json(path.parent / "report.json")["status"] != "passed":
            raise ValueError("Only compatible, independently validated episode manifests may be combined")
        for episode in manifest["episodes"]:
            if digest(episode["dataset"]) != episode["datasetSha256"] or digest(episode["sourceTrace"]) != episode["sourceTraceSha256"]:
                raise ValueError("Source checksum changed: " + episode["matchId"])
            episodes.append(episode)
            run_path = Path(episode["sourceTrace"]).parent.parent / "run.json"
            runs[str(run_path)] = read_json(run_path)
    metadata = next(iter(runs.values()))
    card_ids = sorted(card["CardId"] for card in metadata["cardDefinitions"])
    if len(card_ids) != 41 or any(sorted(card["CardId"] for card in run["cardDefinitions"]) != card_ids for run in runs.values()):
        raise ValueError("Public card catalogs differ")
    spec = {"encoder_version": ENCODER_VERSION, "ai_schema": 2, "card_ids": card_ids,
            "zone_values": sorted(item["value"] for item in metadata["aiContract"]["zones"]),
            "action_type_values": sorted(item["value"] for item in metadata["aiContract"]["actionTypes"]),
            "unknown_card_index": 0}
    groups = {int(episode["seed"]) for episode in episodes}
    validation_seeds = set(validation_seeds)
    test_seeds = set(test_seeds or [])
    teachers = set(teachers or ["Normal"])
    held_out = validation_seeds | test_seeds
    if (not validation_seeds or validation_seeds & test_seeds or not held_out < groups
            or not teachers):
        raise ValueError("Validation/test seeds must be disjoint, held out by whole seed, and leave training data")
    output.mkdir(parents=True)
    split_names = ["train", "validation"] + (["test"] if test_seeds else [])
    counts = {"teacher_decisions": 0, "excluded_teacher": 0, "forced_excluded": 0,
              **{name: 0 for name in split_names}}
    teacher_counts = {teacher: 0 for teacher in sorted(teachers)}
    seen = set()
    observed = set()
    used_sources = {}
    split_episodes = {name: [] for name in split_names}
    widths = None
    try:
        with ExitStack() as stack:
            streams = {name: stack.enter_context((output / f"{name}.jsonl").open("w", encoding="utf-8"))
                       for name in split_names}
            for episode in episodes:
                seed = int(episode["seed"])
                split = "test" if seed in test_seeds else ("validation" if seed in validation_seeds else "train")
                episode_key = str(Path(episode["sourceTrace"]).parent.parent.name) + "/" + episode["matchId"]
                split_episodes[split].append(episode_key)
                with Path(episode["dataset"]).open(encoding="utf-8-sig") as stream:
                    for line in stream:
                        record = json.loads(line)
                        if record["record"] != "transition":
                            continue
                        bot = episode["bot0"] if record["player"] == 0 else episode["bot1"]
                        if bot not in teachers:
                            counts["excluded_teacher"] += 1
                            continue
                        counts["teacher_decisions"] += 1
                        teacher_counts[bot] += 1
                        policy = json.loads(record["policyInputJson"])
                        observed.update(entity["cardId"] for entity in policy["entities"] if entity["cardId"])
                        chosen = policy["legalActions"][record["selectedActionIndex"]]
                        if chosen["source"] >= 0:
                            identity = policy["entities"][chosen["source"]]["cardId"]
                            key = identity + "/" + str(chosen["features"][0])
                            used_sources[key] = used_sources.get(key, 0) + 1
                        if len(policy["legalActions"]) < min_choices:
                            counts["forced_excluded"] += 1
                            continue
                        key = (episode_key, record["player"], record["sequence"])
                        if key in seen or record["truncated"]:
                            raise ValueError("Duplicate or truncated teacher sample")
                        seen.add(key)
                        context, actions = encode(policy, spec)
                        shape = (len(context), len(actions[0]))
                        if widths is not None and widths != shape:
                            raise ValueError("Encoder widths changed")
                        widths = shape
                        sample = {"schema": 1, "episode": episode_key, "group_seed": seed, "teacher": bot,
                                  "player": record["player"], "sequence": record["sequence"],
                                  "selected_action_index": record["selectedActionIndex"], "policy": policy}
                        streams[split].write(json.dumps(sample, separators=(",", ":"), allow_nan=False) + "\n")
                        counts[split] += 1
        if any(not counts[name] for name in split_names):
            raise ValueError("Every requested grouped split needs meaningful teacher choices")
        spec.update(context_size=widths[0], action_size=widths[1])
        (output / "feature_spec.json").write_text(json.dumps(spec, indent=2), encoding="utf-8")
        purpose = "imitation dataset with independent grouped test" if test_seeds else "imitation pipeline smoke; no held-out test set"
        report = {"schema": 1, "status": "prepared", "purpose": purpose,
                  "counts": counts, "teachers": sorted(teachers), "teacher_transition_counts": teacher_counts,
                  "train_seeds": sorted(groups - held_out), "validation_seeds": sorted(validation_seeds),
                  "test_seeds": sorted(test_seeds),
                  "split_episodes": split_episodes, "teacher_public_card_ids": sorted(observed),
                  "teacher_selected_source_counts_by_card_and_action_enum": used_sources,
                  "rules_version": version[0], "catalog_version": version[1], "ai_schema": version[2],
                  "source_fingerprint": version[3], "catalog_fingerprint": version[4],
                  "input_manifests": [{"path": str(path), "sha256": digest(path)} for path, _ in manifests],
                  "files": {name: digest(output / name) for name in
                            ([f"{split}.jsonl" for split in split_names] + ["feature_spec.json"])},
                  "cautions": ["Group/seed/teacher/provenance are for splitting and reports only; encode receives policy only.",
                               "Accuracy on forced single-action decisions is excluded; imitation accuracy is not a game win rate.",
                               "Unknown card IDs map to UNK; this preserves encoding shape, not guaranteed playing strength."]}
        (output / "training_manifest.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        return report
    except Exception as error:
        (output / "PREPARATION_FAILED.txt").write_text(str(error), encoding="utf-8")
        raise


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--episode-manifest", action="append", required=True)
    parser.add_argument("--validation-seed", type=int, action="append", required=True)
    parser.add_argument("--test-seed", type=int, action="append")
    parser.add_argument("--teacher", action="append", help="Allowed teacher bot name; defaults to Normal")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    result = prepare(args.episode_manifest, args.output, args.validation_seed, args.test_seed, args.teacher)
    print(json.dumps({key: result[key] for key in
                      ("status", "counts", "teachers", "train_seeds", "validation_seeds", "test_seeds")}))
