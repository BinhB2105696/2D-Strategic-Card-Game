"""Export a frozen rules-v6 policy and its Python parity fixtures."""
import argparse
import hashlib
import json
import struct
from pathlib import Path

import torch

from action_features import ENCODER_VERSION, encode
from policy_model import ActionScorer


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def dataset_files(selection, *names):
    dataset = Path(selection["datasetPath"])
    manifest_path = dataset / "training_manifest.json"
    if sha256(manifest_path) != selection["datasetManifestSha256"]:
        raise ValueError("Selected dataset manifest changed")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    for name in names:
        if sha256(dataset / name) != manifest["files"][name]:
            raise ValueError(f"Selected dataset file changed: {name}")
    return dataset


@torch.inference_mode()
def parity_fixture(model, spec, policy, source):
    context, candidates = encode(policy, spec)
    logits = model(torch.tensor([context], dtype=torch.float32),
                   torch.tensor([candidates], dtype=torch.float32),
                   torch.ones((1, len(candidates)), dtype=torch.bool))[0]
    return {"source": source, "policy": policy, "logits": logits.tolist(), "selected": int(logits.argmax())}


def export(selection_path, output, candidate="mixed_hard_normal"):
    selection = json.loads(selection_path.read_text(encoding="utf-8-sig"))
    versions = tuple(selection[key] for key in ("rulesVersion", "catalogVersion", "aiSchema"))
    if selection["status"] != "selected" or versions != (6, 4, 3):
        raise ValueError("Expected the frozen rules-v6 selection")
    checkpoints = {item["dataset"]: item for item in selection["selected"]}
    chosen = checkpoints[candidate]
    checkpoint = Path(chosen["checkpoint"])
    dataset = dataset_files(chosen, "feature_spec.json")
    # Use identical reference states for all candidates, including retaliation cases
    # absent from an individual teacher's validation trajectories.
    fixture_dataset = dataset_files(checkpoints["mixed_hard_normal"], "validation.jsonl")

    saved = torch.load(checkpoint, map_location="cpu", weights_only=True)
    spec = json.loads((dataset / "feature_spec.json").read_text(encoding="utf-8"))
    if (saved["schema"] != 1 or saved["epoch"] != chosen["bestEpoch"]
            or saved["config"]["seed"] != chosen["seed"]
            or abs(saved["best_validation_loss"] - chosen["validationLoss"]) > 1e-8
            or saved["feature_spec"] != spec or spec["ai_schema"] != versions[2]
            or spec["encoder_version"] != ENCODER_VERSION or spec["unknown_card_index"] != 0):
        raise ValueError(f"Checkpoint does not match the selected {candidate} policy")
    model = ActionScorer(spec["context_size"], spec["action_size"], saved["config"]["hidden_sizes"])
    model.load_state_dict(saved["model_state"])
    model.eval()
    torch.set_num_threads(1)

    fixtures = []
    coverage = set()
    incoming_policy = None
    with (fixture_dataset / "validation.jsonl").open(encoding="utf-8") as stream:
        for line in stream:
            sample = json.loads(line)
            policy = sample["policy"]
            actions = policy["legalActions"]
            marks = {(mark["source"], mark["target"]) for mark in policy["retaliationMarks"]}
            tags = {f"action-{int(action['features'][0])}" for action in actions}
            if any(not entity["cardId"] for entity in policy["entities"]):
                tags.add("unknown-card")
            if any(len(action["targets"]) > 1 for action in actions):
                tags.add("multiple-targets")
            if any(len(action["tributes"]) > 1 for action in actions):
                tags.add("multiple-tributes")
            if marks:
                tags.add("retaliation")
            for action in actions:
                for target in action["targets"]:
                    if (action["source"], target) in marks:
                        tags.add("outgoing")
                    if (target, action["source"]) in marks:
                        tags.add("incoming")
                        if incoming_policy is None:
                            incoming_policy = policy
            if len(fixtures) >= 12 and tags <= coverage:
                continue
            coverage.update(tags)
            fixtures.append(parity_fixture(model, spec, policy, "validation"))
    if not {"unknown-card", "retaliation", "incoming"} <= coverage:
        raise ValueError(f"Missing parity fixture coverage: {sorted(coverage)}")
    if "outgoing" not in coverage:
        reversed_marks = [dict(mark, source=mark["target"], target=mark["source"])
                          for mark in incoming_policy["retaliationMarks"]]
        policy = dict(incoming_policy, retaliationMarks=reversed_marks)
        fixtures.append(parity_fixture(model, spec, policy, "synthetic-reversed-retaliation"))
        coverage.add("outgoing")

    output.mkdir(parents=True, exist_ok=True)
    model_path = write_policy_artifact(model, spec, versions, output)
    (output / "parity.json").write_text(json.dumps(fixtures, allow_nan=False), encoding="utf-8")
    (output / "export.json").write_text(json.dumps({
        "artifactVersion": 1, "checkpointSha256": sha256(checkpoint),
        "artifactSha256": sha256(model_path), "datasetManifestSha256": chosen["datasetManifestSha256"],
        "dataset": chosen["dataset"], "seed": chosen["seed"], "epoch": chosen["bestEpoch"],
        "featureSpec": spec,
        "parityDataset": "mixed_hard_normal",
        "validationFixtures": sum(fixture["source"] == "validation" for fixture in fixtures),
        "syntheticFixtures": sum(fixture["source"] != "validation" for fixture in fixtures),
        "fixtureCoverage": sorted(coverage),
    }, indent=2) + "\n", encoding="utf-8")
    print(f"{model_path} ({len(fixtures)} parity fixtures)")


def write_policy_artifact(model, spec, versions, output):
    artifact = bytearray(b"LCBP")

    def integers(*values):
        artifact.extend(struct.pack(f"<{len(values)}i", *values))

    integers(1, *versions, ENCODER_VERSION, spec["global_feature_count"])
    integers(len(spec["card_ids"]))
    for card_id in spec["card_ids"]:
        encoded_id = card_id.encode("utf-8")
        integers(len(encoded_id))
        artifact.extend(encoded_id)
    for key in ("zone_values", "action_type_values"):
        integers(len(spec[key]), *spec[key])
    layers = list(model.network[::2])
    integers(len(layers))
    for layer in layers:
        integers(layer.in_features, layer.out_features)
        for tensor in (layer.weight, layer.bias):
            values = tensor.detach().flatten()
            if not torch.isfinite(values).all():
                raise ValueError("Non-finite model weights")
            artifact.extend(struct.pack(f"<{values.numel()}f", *values.tolist()))
    model_path = output / "learned-policy-v1.bin"
    model_path.write_bytes(artifact)
    return model_path


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("selection_path", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--candidate", choices=("mixed_hard_normal", "normal_only", "hard_only"),
                        default="mixed_hard_normal")
    export(**vars(parser.parse_args()))
