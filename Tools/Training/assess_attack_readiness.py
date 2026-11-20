"""Measure conditional attack-readiness rankings against verified rules-v6 replays."""
import argparse
from collections import Counter, defaultdict
import json
from pathlib import Path

import torch
from torch.nn import functional as F

from action_features import (BOARD_CONTEXT_SIZE, FUNDAMENTALS_ACTION_SIZE, FUNDAMENTALS_ENCODER_VERSION,
                             can_prepare_attack, encode, energy_shortfall)
from export_learned_policy import dataset_files, parity_fixture, write_policy_artifact
from fundamentals_replay import read_games, records
from policy_model import ActionScorer
from train_imitation import JsonlPolicyDataset, collate_policy_batch, limit_dataset, make_loader, read_json, run_epoch, sha256
from train_learned_fundamentals import new_model


def learner_rollout_games(directory, catalog):
    """The paired rollout collections predate both models' fine-tuning seeds."""
    for name in ("learner-v1", "learner-v1-extension-1", "learner-v1-extension-2"):
        receipt = read_json(directory / name / "verified-collection.json")
        run = Path(receipt["runDirectory"]).resolve()
        batch = read_json(receipt["replayReport"])
        metadata = read_json(run / "run.json")
        if (batch["status"] != "completed" or batch["verifiedMatches"] != 48 or len(batch["results"]) != 48
                or Path(batch["sourceRun"]).resolve() != run or metadata["catalogFingerprint"] != catalog
                or (metadata["rulesVersion"], metadata["catalogVersion"], metadata["aiContract"]["schema"]) != (6, 4, 3)):
            raise ValueError(f"Incompatible rollout batch: {run}")
        for entry in batch["results"]:
            verified = read_json(entry["report"])
            trace = (run / "traces" / entry["trace"]).resolve()
            dataset = Path(entry["dataset"]).resolve()
            if (trace.parent != run / "traces" or not verified["verified"] or not verified["completeMatch"]
                    or verified["truncated"] or Path(verified["trace"]).resolve() != trace
                    or Path(verified["dataset"]).resolve() != dataset):
                raise ValueError(f"Unverified replay: {trace}")
            replay = records(dataset)
            header = next(replay)
            if (header["schema"] != 3 or header["catalogFingerprint"] != catalog
                    or header["sourceFingerprint"] != metadata["sourceFingerprint"]
                    or Path(header["sourceTrace"]).resolve() != trace):
                raise ValueError(f"Incompatible replay header: {trace}")
            transitions = {r["sequence"]: r for r in replay if r["record"] == "transition"}
            log = records(trace)
            start = next(log)
            side = 0 if start["bot0"] == "Learned" else 1
            if (start["matchId"] != header["matchId"] or start[f"bot{side}"] != "Learned"
                    or start[f"bot{1-side}"] != "Normal" or len(transitions) != verified["verifiedDecisions"]):
                raise ValueError(f"Unexpected rollout match: {trace}")
            attempts = {}
            for row in log:
                seq = row.get("sequence")
                if row["record"] == "attempt" and seq in transitions:
                    data = transitions[seq]
                    if (row["automatic"] or row["action"]["player"] != data["player"]
                            or row["policyInputJson"] != data["policyInputJson"]
                            or row["selectedActionIndex"] != data["selectedActionIndex"]):
                        raise ValueError(f"Unverified action at {trace}:{seq}")
                    row["policy"] = json.loads(data["policyInputJson"])
                    attempts[seq] = row
                if row["record"] == "accepted" and seq in attempts:
                    attempts[seq]["result"] = row
            deck = int(start["matchId"].split("_")[0][1:])
            yield start["matchId"], start["seed"], side, "Normal", deck, None, list(attempts.values())


def readiness_cases(game):
    match, seed, learner, opponent, deck, _, steps = game
    learner_steps = [step for step in steps if step["action"]["player"] == learner]
    turns_entered = {step["turn"] for step in learner_steps if any(
        event["Type"] == "PhaseChanged" and event["Phase"] == "Battle" for event in step["result"]["events"])}
    attacks = {step["turn"] for step in learner_steps if step["action"]["type"] == "Attack"}
    for step in learner_steps:
        policy = step["policy"]
        if step["turn"] <= 1 or step["turn"] not in turns_entered or policy["globalFeatures"][1:6] != [2, 1, 1, 0, 0]:
            continue
        entities = policy["entities"]

        def eligible(entity):
            f = entity["features"]
            return f[0] == 0 and can_prepare_attack(f) and not (entity["cardId"] == "logistics_squire" and f[17])

        if any(eligible(e) and energy_shortfall(e["features"]) == 0 for e in entities):
            continue
        completions, incomplete = defaultdict(list), defaultdict(list)
        phase = []
        for i, action in enumerate(policy["legalActions"]):
            kind = action["features"][0]
            if kind == 2:
                phase.append(i)
            if kind != 5 or action["source"] < 0:
                continue
            monster = entities[action["source"]]
            # Compare allocations to otherwise eligible attackers in the same state.
            if eligible(monster):
                amount = round(5 * action["features"][2])
                target = completions if 0 < energy_shortfall(monster["features"]) <= amount else incomplete
                target[amount].append(i)
        if not completions:
            continue
        key = (match, seed, deck, opponent, step["sequence"], step["turn"], step["selectedActionIndex"],
               step["turn"] in attacks)
        common_costs = completions.keys() & incomplete.keys()
        if common_costs:
            cost = min(common_costs)
            yield key, "equal_cost", policy, completions[cost], incomplete[cost]
        if phase and step["selectedActionIndex"] in phase:
            yield key, "observed_early_battle", policy, completions[min(completions)], phase


def evaluate(cases, models):
    scores = defaultdict(Counter)
    games = defaultdict(set)
    seeds = defaultdict(set)
    decks = defaultdict(set)
    for name, (model, feature_spec) in models.items():
        encoded = [(*encode(case[2], feature_spec), 0) for case in cases]
        for start in range(0, len(cases), 64):
            contexts, actions, mask, _ = collate_policy_batch(encoded[start:start+64])
            with torch.inference_mode():
                logits = model(contexts, actions, mask)
            for row, case in zip(logits, cases[start:start+64], strict=True):
                (match, seed, deck, opponent, _, _, selected, attacked), kind, _, positive, other = case
                picked = int(row.argmax())
                margin = (row[positive].max() - row[other].max()).item()
                for subset in ("All", opponent):
                    group = name, kind, subset
                    scores[group].update({"states": 1, "prefers_readiness": int(margin > 1e-6),
                                          "tie": int(abs(margin) <= 1e-6), "greedy_other": int(picked in other),
                                          "played_other": int(selected in other), "turn_had_attack": int(attacked)})
                    games[group].add((seed, match))
                    seeds[group].add(seed)
                    decks[group].add(deck)
    return [{"model": key[0], "scenario": key[1], "opponent": key[2], **counts,
             "matches": len(games[key]), "seeds": len(seeds[key]),
             "decks": sorted(decks[key])} for key, counts in sorted(scores.items())]


def teach(cases, validation, independent, models, parent, selection, output):
    if output.exists():
        raise ValueError(f"Targeted pilot already exists: {output}")
    original, = [row for row in read_json(selection)["selected"] if row["dataset"] == "mixed_hard_normal"]
    anchor = dataset_files(original, "feature_spec.json", "train.jsonl", "validation.jsonl")
    reference, old_spec = models["reward_v1"]
    if read_json(anchor / "feature_spec.json") != old_spec:
        raise ValueError("Unexpected anchor feature specification")
    spec = dict(old_spec, encoder_version=FUNDAMENTALS_ENCODER_VERSION,
                context_size=old_spec["context_size"] + BOARD_CONTEXT_SIZE,
                action_size=old_spec["action_size"] + FUNDAMENTALS_ACTION_SIZE)
    examples = [case for case in cases if case[1] == "equal_cost"]
    if not examples or not validation or not independent:
        raise ValueError("Targeted training and both validation sets must have data")
    model = new_model(spec, old_spec, reference.state_dict())
    for parameter in model.parameters():
        parameter.requires_grad_(False)
    weight = model.network[0].weight
    weight.requires_grad_(True)
    columns = spec["context_size"] + old_spec["action_size"]
    mask = torch.zeros_like(weight)
    mask[:, [columns, columns + 1, columns + 3]] = 1
    weight.register_hook(lambda grad: grad * mask)
    optimizer = torch.optim.Adam([weight], lr=0.002)
    context, actions, legal, _ = collate_policy_batch([(*encode(case[2], spec), 0) for case in examples])
    original_samples = limit_dataset(JsonlPolicyDataset(anchor / "train.jsonl", spec), 768, 20261120)
    anchor_batches = make_loader(original_samples, 64, True, 20261120, 0)
    model.train()
    for c0, a0, m0, y0 in anchor_batches:
        logits = model(context, actions, legal)
        margins = torch.stack([row[case[3]].max() - row[case[4]].max()
                               for row, case in zip(logits, examples, strict=True)])
        loss = F.softplus(-margins).mean() + F.cross_entropy(model(c0, a0, m0), y0)
        optimizer.zero_grad(set_to_none=True)
        loss.backward()
        optimizer.step()
    model.eval()
    comparison = {"reward_v1": models["reward_v1"], "targeted": (model, spec)}
    results = {name: evaluate(data, comparison) for name, data in (("validation", validation), ("independent", independent))}
    anchor_scores = {}
    for name, (policy, encoding) in comparison.items():
        examples_for_check = limit_dataset(JsonlPolicyDataset(anchor / "validation.jsonl", encoding), 1024, 20261120)
        anchor_scores[name] = run_epoch(policy, make_loader(examples_for_check, 128, False, 20261120, 0), "cpu")

    def improved(rows):
        old, new = [next(r for r in rows if r["model"] == name and r["scenario"] == "equal_cost" and r["opponent"] == "All")
                    for name in ("reward_v1", "targeted")]
        return new["prefers_readiness"] > old["prefers_readiness"] and new["greedy_other"] <= old["greedy_other"]

    passed = (all(improved(rows) for rows in results.values())
              and anchor_scores["targeted"]["top1"] >= anchor_scores["reward_v1"]["top1"] - 0.02)
    output.mkdir(parents=True)
    if passed:
        candidate = output / "candidate"
        candidate.mkdir()
        torch.save(model.state_dict(), candidate / "best.pt")
        artifact = write_policy_artifact(model, spec, (6, 4, 3), candidate)
        fixtures = [parity_fixture(model, spec, row["policy"], row["source"]) for row in read_json(parent / "parity.json")]
        fixtures += [parity_fixture(model, spec, case[2], f"{case[0][0]}:{case[0][4]}") for case in independent[:8]]
        (candidate / "parity.json").write_text(json.dumps(fixtures, allow_nan=False), encoding="utf-8")
        export = {"dataset": "learned_attack_readiness_v1", "artifactVersion": 1,
                  "artifactSha256": sha256(artifact), "checkpointSha256": sha256(candidate / "best.pt"),
                  "baselineSha256": read_json(parent / "export.json")["artifactSha256"], "featureSpec": spec}
        (candidate / "export.json").write_text(json.dumps(export, indent=2), encoding="utf-8")
    report = {"train_pairs": len(examples), "train_seeds": sorted({case[0][1] for case in examples}),
              "learning_rate": 0.002, "updates": len(anchor_batches), "passed": passed, "anchor": anchor_scores, **results}
    (output / "report.json").write_text(json.dumps(report, indent=2, allow_nan=False), encoding="utf-8")
    print(f"Targeted pilot trained on {len(examples)} pairs. Offline gate: {passed}. {output}")


def main(train_target):
    torch.set_num_threads(2)
    base = Path("SimulationResults")
    reward = base / "LearnedReward/v1/candidate"
    pilot = base / "LearnedFundamentals/v1"
    models = {}
    for name, path in (("reward_v1", reward), ("fundamentals", pilot / "candidate")):
        export = read_json(path / "export.json")
        feature_spec = export["featureSpec"]
        checkpoint = path / "best.pt"
        if sha256(checkpoint) != export["checkpointSha256"]:
            raise ValueError(f"Frozen model changed: {checkpoint}")
        scorer = ActionScorer(feature_spec["context_size"], feature_spec["action_size"], [128, 64])
        scorer.load_state_dict(torch.load(checkpoint, map_location="cpu", weights_only=True))
        scorer.eval()
        models[name] = (scorer, feature_spec)
    policy_hash = read_json(reward / "export.json")["artifactSha256"]
    collection = base / "LearnedReward/v3/verified-collection.json"
    catalog = read_json(Path(read_json(collection)["runDirectory"]) / "run.json")["catalogFingerprint"]
    holdout_seeds = set(read_json(pilot / "report.json")["validation_seeds"])
    result = {}
    replay_cases = {}
    for name, corpus in (("reward_v3", read_games(collection, policy_hash)),
                         ("independent_rollouts", learner_rollout_games(base / "LearnedRollout", catalog))):
        cases = [case for game in corpus for case in readiness_cases(game)]
        replay_cases[name] = cases
        result[name] = evaluate(cases, models)
        if name == "reward_v3":
            result["fundamentals_validation_seeds"] = evaluate([case for case in cases if case[0][1] in holdout_seeds], models)
    path = pilot / "attack-readiness.json"
    path.write_text(json.dumps(result, indent=2, allow_nan=False), encoding="utf-8")
    for corpus, rows in result.items():
        print(corpus)
        for row in rows:
            if row["opponent"] in ("All", "Hard"):
                print(json.dumps(row))
    print(f"Output: {path}")
    if train_target:
        fresh = replay_cases["independent_rollouts"]
        original = replay_cases["reward_v3"]
        if {case[0][1] for case in fresh} & {case[0][1] for case in original}:
            raise ValueError("Independent assessment overlaps training seeds")
        teach([case for case in original if case[0][1] not in holdout_seeds],
              [case for case in original if case[0][1] in holdout_seeds], fresh, models, reward,
              base / "TrainingRuns/rules-v6-imitation_20261119_083127/selection.json",
              base / "LearnedFundamentals/attack-readiness-v1")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--teach", action="store_true")
    main(parser.parse_args().teach)
