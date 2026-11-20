"""Compare replay lessons with and without explicit, rule-derived board inputs."""
import argparse
import json
from collections import Counter, defaultdict
from pathlib import Path

import torch
from torch.nn import functional as F
from torch.utils.data import DataLoader

from action_features import BOARD_CONTEXT_SIZE, FUNDAMENTALS_ACTION_SIZE, FUNDAMENTALS_ENCODER_VERSION, encode
from export_learned_policy import dataset_files, parity_fixture, write_policy_artifact
from fundamentals_replay import extract, write_scorecard
from train_imitation import JsonlPolicyDataset, collate_policy_batch, limit_dataset, make_loader, read_json, run_epoch, sha256
from policy_model import ActionScorer


SEED = 20261120
BATCH = 64
EPOCHS = 3


def lesson_batch(samples):
    context, actions, legal, _ = collate_policy_batch([(c, a, p[0]) for c, a, p, _ in samples])
    preferred = torch.zeros_like(legal)
    for i, (_, _, indices, _) in enumerate(samples):
        preferred[i, indices] = True
    return context, actions, legal, preferred, [skill for _, _, _, skill in samples]


def new_model(spec, original_spec, parent):
    model = ActionScorer(spec["context_size"], spec["action_size"], [128, 64])
    initial = dict(parent)
    if spec != original_spec:
        # Preserve v1 logits at initialization. The only extra parameters initially contribute zero.
        weight = torch.zeros_like(model.network[0].weight)
        old_context = original_spec["context_size"]
        old_action = original_spec["action_size"]
        weight[:, :old_context] = parent["network.0.weight"][:, :old_context]
        start = spec["context_size"]
        weight[:, start:start + old_action] = parent["network.0.weight"][:, old_context:]
        initial["network.0.weight"] = weight
    model.load_state_dict(initial)
    return model


def encode_lessons(rows, spec):
    samples = []
    for _, _, skill, policy, preferred, _, _ in rows:
        context, actions = encode(policy, spec)
        samples.append((context, actions, preferred, skill))
    return samples


@torch.inference_mode()
def measure(model, samples):
    model.eval()
    scores = defaultdict(Counter)
    for context, actions, legal, preferred, skills in DataLoader(samples, BATCH, collate_fn=lesson_batch):
        logits = model(context, actions, legal)
        picked = logits.argmax(dim=1, keepdim=True)
        hits = preferred.gather(1, picked).flatten().tolist()
        losses = (logits.logsumexp(1) - logits.masked_fill(~preferred, -torch.inf).logsumexp(1)).tolist()
        for name, hit, loss in zip(skills, hits, losses, strict=True):
            for key in (name, "all"):
                scores[key]["choices"] += 1
                scores[key]["hits"] += hit
                scores[key]["total_loss"] += loss
    return {key: {"choices": s["choices"], "hits": s["hits"], "accuracy": s["hits"] / s["choices"],
                  "loss": s["total_loss"] / s["choices"]} for key, s in scores.items()}


def fit(model, train, anchor, spec):
    counts = Counter(sample[3] for sample in train)
    optimizer = torch.optim.AdamW(model.parameters(), lr=1e-4, weight_decay=1e-4)
    original = JsonlPolicyDataset(anchor / "train.jsonl", spec)
    for epoch in range(EPOCHS):
        rng = torch.Generator().manual_seed(SEED + epoch)
        batches = DataLoader(train, BATCH, shuffle=True, generator=rng, collate_fn=lesson_batch)
        base = make_loader(limit_dataset(original, len(train), SEED + epoch), BATCH, True, SEED + epoch, 0)
        model.train()
        for lesson, reference in zip(batches, base, strict=True):
            context, actions, legal, preferred, skills = lesson
            logits = model(context, actions, legal)
            losses = logits.logsumexp(1) - logits.masked_fill(~preferred, -torch.inf).logsumexp(1)
            weights = torch.tensor([counts[skill] ** -0.5 for skill in skills], dtype=losses.dtype)
            c0, a0, m0, y0 = reference
            loss = (weights * losses).sum() / weights.sum() + F.cross_entropy(model(c0, a0, m0), y0)
            optimizer.zero_grad(set_to_none=True)
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.parameters(), 1)
            optimizer.step()


def train(collection, parent, selection, output):
    torch.set_num_threads(4)
    torch.use_deterministic_algorithms(True)
    if (output / "report.json").exists() or (output / "candidate").exists():
        raise ValueError("Fundamentals pilot output already exists")
    frozen = read_json(parent / "export.json")
    reference_spec = frozen["featureSpec"]
    weight_path = parent / "best.pt"
    if (frozen["dataset"] != "full_game_reward_v1" or reference_spec["encoder_version"] != 1
            or sha256(weight_path) != frozen["checkpointSha256"]
            or sha256(parent / "learned-policy-v1.bin") != frozen["artifactSha256"]):
        raise ValueError("Expected the frozen Reward v1 artifact")
    state = torch.load(weight_path, map_location="cpu", weights_only=True)
    original, = [row for row in read_json(selection)["selected"] if row["dataset"] == "mixed_hard_normal"]
    anchor = dataset_files(original, "feature_spec.json", "train.jsonl", "validation.jsonl")
    if read_json(anchor / "feature_spec.json") != reference_spec:
        raise ValueError("Supervised anchor and starting policy encoders differ")
    activity, games, lessons = extract(collection, frozen["artifactSha256"])
    output.mkdir(parents=True, exist_ok=True)
    write_scorecard(output / "action-scorecard.csv", activity, games)
    seeds = sorted({row[0] for row in lessons})
    if len(seeds) != 16:
        raise ValueError("Expected lessons from 16 separate seed blocks")
    valid_seeds = set(seeds[::4])
    training = [row for row in lessons if row[0] not in valid_seeds]
    validation = [row for row in lessons if row[0] in valid_seeds]
    source_seeds = read_json(anchor / "training_manifest.json")
    if set(seeds) & {seed for split in ("train", "validation", "test") for seed in source_seeds[f"{split}_seeds"]}:
        raise ValueError("Tactical seeds overlap the existing imitation dataset")
    print(f"Scorecard written. Lessons: {len(training)} train / {len(validation)} held out.", flush=True)

    board_spec = dict(reference_spec, encoder_version=FUNDAMENTALS_ENCODER_VERSION,
                      context_size=reference_spec["context_size"] + BOARD_CONTEXT_SIZE,
                      action_size=reference_spec["action_size"] + FUNDAMENTALS_ACTION_SIZE)
    reports = {}
    candidates = {}
    validation_policies = []
    for name, spec in (("lessons", reference_spec), ("lessons_and_board", board_spec)):
        model = new_model(spec, reference_spec, state)
        valid = encode_lessons(validation, spec)
        legacy = limit_dataset(JsonlPolicyDataset(anchor / "validation.jsonl", spec), 1024, SEED)
        loader = make_loader(legacy, 128, False, SEED, 0)
        if name == "lessons":
            reports["reward_v1"] = {"lessons": measure(model, valid), "anchor": run_epoch(model, loader, "cpu")}
            validation_policies = [(policy, match, seq) for _, _, _, policy, _, match, seq in validation[:12]]
        fit(model, encode_lessons(training, spec), anchor, spec)
        reports[name] = {"lessons": measure(model, valid), "anchor": run_epoch(model, loader, "cpu")}
        candidates[name] = (model, spec)
        results = reports[name]["lessons"]["all"]
        print(f"{name}: {results['hits']}/{results['choices']} held-out lesson choices.", flush=True)

    baseline = reports["reward_v1"]
    passed = [name for name in candidates
              if reports[name]["lessons"]["all"]["accuracy"] > baseline["lessons"]["all"]["accuracy"]
              and reports[name]["anchor"]["top1"] >= baseline["anchor"]["top1"] - 0.02]
    selected = max(passed, key=lambda name: (reports[name]["lessons"]["all"]["accuracy"],
                                             -reports[name]["lessons"]["all"]["loss"])) if passed else None
    if selected:
        model, spec = candidates[selected]
        candidate = output / "candidate"
        candidate.mkdir()
        torch.save(model.state_dict(), candidate / "best.pt")
        artifact = write_policy_artifact(model, spec, (6, 4, 3), candidate)
        fixtures = [parity_fixture(model, spec, row["policy"], row["source"]) for row in read_json(parent / "parity.json")]
        fixtures += [parity_fixture(model, spec, policy, f"{match}:{seq}") for policy, match, seq in validation_policies]
        (candidate / "parity.json").write_text(json.dumps(fixtures, allow_nan=False), encoding="utf-8")
        export = {"dataset": "learned_fundamentals_v1", "epoch": EPOCHS, "artifactVersion": 1,
                  "artifactSha256": sha256(artifact), "checkpointSha256": sha256(candidate / "best.pt"),
                  "baselineSha256": frozen["artifactSha256"], "featureSpec": spec}
        (candidate / "export.json").write_text(json.dumps(export, indent=2), encoding="utf-8")
    report = {"collection": str(collection), "policy_sha256": frozen["artifactSha256"], "train_seeds": sorted(set(seeds) - valid_seeds),
              "validation_seeds": sorted(valid_seeds), "lesson_count": dict(Counter(row[2] for row in lessons)),
              "metrics": reports, "selected": selected}
    (output / "report.json").write_text(json.dumps(report, indent=2, allow_nan=False), encoding="utf-8")
    print(f"Selected for gameplay: {selected or 'none'}", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("collection", "parent", "selection", "output"):
        parser.add_argument(name, type=Path)
    train(**vars(parser.parse_args()))
