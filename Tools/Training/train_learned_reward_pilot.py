"""One on-policy policy-gradient update using verified full-game rewards."""
import argparse
from collections import Counter, defaultdict
import json
import math
from pathlib import Path
from statistics import mean

import torch
from torch.nn import functional as F
from torch.utils.data import DataLoader

from action_features import encode
from export_learned_policy import dataset_files, parity_fixture, write_policy_artifact
from policy_model import ActionScorer
from train_imitation import (
    JsonlPolicyDataset, collate_policy_batch, limit_dataset, make_loader, read_json, run_epoch, sha256,
)


EXPLORATION = 0.05
SEED = 20261122
EPOCHS = 2
BATCH = 64
CLIP = 0.15
ANCHOR_WEIGHT = 0.25
GAE_LAMBDA = 0.95


def records(path):
    with Path(path).open(encoding="utf-8-sig") as stream:
        for line in stream:
            yield json.loads(line)


def next_uint(state):
    state ^= (state << 13) & 0xffffffff
    state ^= state >> 17
    return state ^ ((state << 5) & 0xffffffff)


def collection_schedule(config, round_number=1):
    first, count = config["firstSeed"], config["seedCount"]
    expected_first, exploration, strong = {
        1: (2026112281, 7413927, "Learned"),
        2: (2026112345, 7413947, "Hard"),
        3: (2026112345, 7413947, "Hard"),
    }[round_number]
    if (first, count, config["learnerExplorationSeed"]) != (expected_first, 16, exploration):
        raise ValueError("Unexpected collection or exploration seeds")
    expected = {(opponent, deck) for opponent in ("Easy", "Normal", strong) for deck in (0, 1)}
    if len(config["matchups"]) != len(expected) or {(m["player1Bot"], m["deck0"]) for m in config["matchups"]} != expected:
        raise ValueError("Unexpected frozen opponent/deck schedule")
    seed = config["learnerExplorationSeed"]
    schedule = {}
    for i, matchup in enumerate(config["matchups"]):
        if matchup["player0Bot"] != "LearnedTrain" or not matchup["swapBots"] or matchup["deck0"] != matchup["deck1"]:
            raise ValueError("Expected paired learner trajectories")
        for match_seed in range(first, first + count):
            for leg in range(2):
                seed = next_uint(seed)
                schedule[f"s{i}_seed{match_seed}_leg{leg}"] = (
                    match_seed, leg, matchup["player1Bot"], matchup["deck0"], seed,
                )
    return schedule


def exploration_profile(round_number):
    return (0.35, 0.02) if round_number == 3 else (1.0, EXPLORATION)


def collect(receipt, frozen_hash, spec, round_number):
    run = Path(receipt["runDirectory"]).resolve()
    result = read_json(receipt["replayReport"])
    meta = read_json(run / "run.json")
    if ((meta["rulesVersion"], meta["catalogVersion"], meta["aiContract"]["schema"]) != (6, 4, 3)
            or meta["learnedPolicySha256"] != frozen_hash or receipt["policySha256"] != frozen_hash
            or meta["sourceFingerprint"] != receipt["sourceFingerprint"]
            or result["status"] != "completed" or result["verifiedMatches"] != 192
            or Path(result["sourceRun"]).resolve() != run or len(result["results"]) != 192):
        raise ValueError("Expected the full replay-verified reward collection")
    for path, expected in receipt["opponentSourceHashes"].items():
        if sha256(path).lower() != expected.lower():
            raise ValueError(f"Frozen opponent source changed: {path}")

    schedule = collection_schedule(meta["config"], round_number)
    temperature, uniform_mix = exploration_profile(round_number)
    if bool(meta["config"].get("lowerLearnerExploration")) != (round_number == 3):
        raise ValueError("Unexpected exploration mode in collection")
    episodes, sources, seen = [], [], set()
    terminal_counts = defaultdict(Counter)
    for entry in result["results"]:
        path = (run / "traces" / entry["trace"]).resolve()
        replay_path = Path(entry["dataset"]).resolve()
        report = read_json(entry["report"])
        if (path in seen or path.parent != run / "traces" or not report["verified"]
                or not report["completeMatch"] or report["truncated"] or Path(report["trace"]).resolve() != path
                or Path(report["dataset"]).resolve() != replay_path):
            raise ValueError(f"Invalid replay verification for {path}")
        seen.add(path)
        exported = records(replay_path)
        source = next(exported)
        if (source["record"] != "dataset_start" or source["schema"] != 3
                or Path(source["sourceTrace"]).resolve() != path
                or source["sourceFingerprint"] != meta["sourceFingerprint"]
                or source["catalogFingerprint"] != meta["catalogFingerprint"]):
            raise ValueError(f"Incompatible replay data: {replay_path}")
        decisions = {row["sequence"]: row for row in exported if row["record"] == "transition"}
        if len(decisions) != report["verifiedDecisions"]:
            raise ValueError(f"Missing or duplicate replay decisions: {replay_path}")
        trace = records(path)
        start = next(trace)
        match_seed, side, opponent, deck, random_state = schedule.pop(start["matchId"])
        profile = start["botProfiles"][side]
        if (start["matchId"] != source["matchId"] or start["seed"] != match_seed
                or start[f"bot{side}"] != "LearnedTrain" or start[f"bot{1 - side}"] != opponent
                or not start["detailed"] or profile["policySha256"] != frozen_hash
                or profile["uniformExploration"] != uniform_mix or profile.get("explorationTemperature", 1.0) != temperature
                or profile["explorationSeed"] != random_state):
            raise ValueError(f"Unexpected exploration policy in {path}")
        if opponent == "Hard":
            hard_profile = start["botProfiles"][1 - side]
            if hard_profile["iterations"] != 96 or hard_profile["learnedRootRanking"]:
                raise ValueError(f"Hard was not the full-strength opponent in {path}")
        endings = [r for r in decisions.values() if r["player"] == side and r["terminated"]]
        if len(endings) != 1 or endings[0]["truncated"] or endings[0]["reward"] not in (-1, 0, 1):
            raise ValueError(f"No verified terminal reward for {path}")
        reward = endings[0]["reward"]
        samples = []
        for row in trace:
            if row["record"] == "end":
                winner = row["result"]["winner"]
                result_reward = 0 if winner == -1 else 1 if winner == side else -1
                if row["result"]["status"] != "Finished" or result_reward != reward or row["result"]["behaviorSamples"] != len(samples):
                    raise ValueError(f"Trajectory reward/sample check failed in {path}")
            if row["record"] != "attempt" or row["automatic"]:
                continue
            learner = row["action"]["player"] == side
            legal = row["legalActions"]
            logp = row.get("behaviorLogProbability")
            if not learner or len(legal) == 1:
                if logp is not None:
                    raise ValueError(f"Unexpected behavior probability in {path}:{row['sequence']}")
                continue
            decision = decisions[row["sequence"]]
            chosen = row["selectedActionIndex"]
            if (decision["player"] != side or decision["policyInputJson"] != row["policyInputJson"]
                    or decision["selectedActionIndex"] != chosen or not 0 <= chosen < len(legal)
                    or logp is None or not math.isfinite(logp) or logp >= 0):
                raise ValueError(f"Unverified behavior decision in {path}:{row['sequence']}")
            context, actions = encode(json.loads(decision["policyInputJson"]), spec)
            if len(actions) != len(legal):
                raise ValueError("Behavior legal-candidate count differs from replay")
            random_state = next_uint(random_state)
            samples.append(((context, actions, chosen), logp, random_state / (1 << 32)))
        if not samples:
            raise ValueError(f"No exploratory decisions in {path}")
        terminal_counts[opponent][reward] += 1
        episodes.append(((opponent, deck, side), reward, samples))
        sources.append({"trace": str(path), "trace_sha256": sha256(path),
                        "replay_sha256": sha256(replay_path), "match_id": start["matchId"], "seed": match_seed})
    if schedule:
        raise ValueError("Missing planned reward trajectories")
    return episodes, dict(terminal_counts), sources


def advantages(episodes):
    groups = defaultdict(list)
    for group, reward, _ in episodes:
        groups[group].append(reward)
    data = []
    for group, reward, samples in episodes:
        rewards = groups[group]
        baseline = (sum(rewards) - reward) / (len(rewards) - 1)
        data.extend((*sample, reward - baseline) for sample in samples)
    return data


def critic_advantages(episodes, sources):
    """Fit the state-value model out of fold, then estimate advantages in decision order."""
    seeds = [source["seed"] for source in sources]
    seed_blocks = sorted(set(seeds))
    if len(seed_blocks) != 16 or len(seeds) != len(episodes):
        raise ValueError("Expected 16 complete reward collection seed blocks")
    targets = torch.tensor([reward for _, reward, _ in episodes], dtype=torch.float64)
    contexts = [torch.tensor([policy[0] for policy, _, _ in samples], dtype=torch.float64)
                for _, _, samples in episodes]
    values = [None] * len(episodes)
    for fold in range(4):
        validation_seeds = set(seed_blocks[fold::4])
        train = [i for i, seed in enumerate(seeds) if seed not in validation_seeds]
        counts = [len(contexts[i]) for i in train]
        states = torch.cat([contexts[i] for i in train])
        rewards = torch.repeat_interleave(targets[train], torch.tensor(counts))
        # Give every training match equal total weight.
        weights = torch.cat([torch.full((count,), 1.0 / (count * len(train)), dtype=states.dtype)
                             for count in counts])
        center = weights @ states
        scale = ((states - center).square().T @ weights).sqrt()
        features = scale > 1e-8
        x = ((states[:, features] - center[features]) / scale[features]).clamp(-8, 8)
        bias = weights @ rewards
        gram = x.T @ (weights[:, None] * x)
        coefficients = torch.linalg.solve(gram + torch.eye(gram.shape[0], dtype=x.dtype),
                                          x.T @ (weights * (rewards - bias)))
        for i, seed in enumerate(seeds):
            if seed in validation_seeds:
                x_test = ((contexts[i][:, features] - center[features]) / scale[features]).clamp(-8, 8)
                values[i] = (bias + x_test @ coefficients).clamp(-1, 1)

    data = []
    for (_, terminal_reward, samples), estimates in zip(episodes, values, strict=True):
        # A nonterminal step ends at the next learner decision with a choice.
        # All intervening moves have zero reward. Gamma is one, as before.
        next_value = float(terminal_reward)
        running = 0.0
        reversed_advantages = []
        for value in reversed(estimates.tolist()):
            running = next_value - value + GAE_LAMBDA * running
            reversed_advantages.append(running)
            next_value = value
        data.extend((*sample, advantage)
                    for sample, advantage in zip(samples, reversed(reversed_advantages), strict=True))
    mse = mean((estimates - target).square().mean().item()
               for estimates, target in zip(values, targets, strict=True))
    print(f"Cross-fitted critic MSE: {mse:.4f} over {len(seed_blocks)} seed blocks.", flush=True)
    return data, mse


def collate(samples):
    policies, old_logp, draws, returns = zip(*samples)
    return (*collate_policy_batch(policies), torch.tensor(old_logp), torch.tensor(draws, dtype=torch.float64),
            torch.tensor(returns))


def behavior_log_probs(logits, mask, temperature=1.0, uniform_mix=EXPLORATION):
    raw = (logits / temperature).log_softmax(dim=1)
    uniform = math.log(uniform_mix) - mask.sum(dim=1, keepdim=True).log()
    return torch.logaddexp(raw + math.log1p(-uniform_mix), uniform).masked_fill(~mask, float("-inf"))


@torch.inference_mode()
def verify_behavior(model, loader, sampling=(1.0, EXPLORATION)):
    model.eval()
    max_error = 0.0
    non_greedy = 0
    for context, actions, mask, chosen, old, draws, _ in loader:
        logits = model(context, actions, mask)
        non_greedy += (logits.argmax(dim=1) != chosen).sum().item()
        probs = behavior_log_probs(logits.double(), mask, *sampling)
        actual = probs.gather(1, chosen[:, None]).squeeze(1)
        error = (actual - old).abs().max().item()
        cdf = probs.exp().cumsum(1)
        upper = cdf.gather(1, chosen[:, None]).squeeze(1)
        lower = upper - actual.exp()
        if error > 0.0001 or ((draws < lower - 0.00002) | (draws > upper + 0.00002)).any():
            raise ValueError("The recorded behavior probabilities or exploration draws failed parity")
        max_error = max(max_error, error)
    return max_error, non_greedy


def update(model, behavior, anchor, optimizer, sampling):
    model.train()
    losses = Counter()
    for (context, actions, mask, chosen, old, _, returns), (c0, a0, m0, y0) in zip(behavior, anchor):
        new = behavior_log_probs(model(context, actions, mask), mask, *sampling).gather(1, chosen[:, None]).squeeze(1)
        log_ratio = new - old
        ratio = log_ratio.exp()
        actor = -torch.minimum(ratio * returns, ratio.clamp(1 - CLIP, 1 + CLIP) * returns).mean()
        anchor_loss = F.cross_entropy(model(c0, a0, m0), y0)
        loss = actor + ANCHOR_WEIGHT * anchor_loss
        optimizer.zero_grad(set_to_none=True)
        loss.backward()
        torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
        optimizer.step()
        losses["actor"] += actor.item()
        losses["anchor"] += anchor_loss.item()
        losses["approx_kl"] += ((ratio - 1) - log_ratio).detach().mean().item()
        losses["batches"] += 1
    if losses["batches"] != len(behavior):
        raise ValueError("The supervised anchor must cover every update batch")
    return {key: losses[key] / losses["batches"] for key in ("actor", "anchor", "approx_kl")}


def train(collection, selection_path, output, round_number=1, use_critic=False):
    if use_critic and round_number != 3:
        raise ValueError("The critic pilot requires the frozen round 3 collection")
    if output.exists():
        raise ValueError("Reward pilot output already exists")
    selection = read_json(selection_path)
    frozen, = [row for row in selection["selected"] if row["dataset"] == "mixed_hard_normal"]
    base = dataset_files(frozen, "feature_spec.json", "train.jsonl", "validation.jsonl")
    artifact = selection_path.parent / "learned-policy-v1"
    exported = read_json(artifact / "export.json")
    checkpoint = Path(frozen["checkpoint"])
    if (sha256(checkpoint) != exported["checkpointSha256"]
            or sha256(artifact / "learned-policy-v1.bin") != exported["artifactSha256"]):
        raise ValueError("Frozen checkpoint checksum changed")
    spec = read_json(base / "feature_spec.json")
    saved = torch.load(checkpoint, weights_only=True, map_location="cpu")
    if saved["feature_spec"] != spec or saved["epoch"] != frozen["bestEpoch"]:
        raise ValueError("Frozen checkpoint metadata changed")
    initial_weights = saved["model_state"]
    if round_number >= 2:
        artifact = collection.parent.parent / "v1" / "candidate"
        exported = read_json(artifact / "export.json")
        checkpoint = artifact / "best.pt"
        if (exported["dataset"] != "full_game_reward_v1" or exported["featureSpec"] != spec
                or sha256(checkpoint) != exported["checkpointSha256"]
                or sha256(artifact / "learned-policy-v1.bin") != exported["artifactSha256"]):
            raise ValueError("Frozen reward v1 checkpoint metadata changed")
        initial_weights = torch.load(checkpoint, weights_only=True, map_location="cpu")
    receipt = read_json(collection)
    episodes, returns, sources = collect(receipt, exported["artifactSha256"], spec, round_number)
    seed = SEED if round_number == 1 else SEED + 1
    sampling = exploration_profile(round_number)
    torch.manual_seed(seed)
    torch.set_num_threads(4)
    torch.use_deterministic_algorithms(True)
    critic_mse = None
    dataset = f"full_game_reward_v{round_number}"
    if use_critic:
        data, critic_mse = critic_advantages(episodes, sources)
        dataset += "_critic"
    else:
        data = advantages(episodes)
    model = ActionScorer(spec["context_size"], spec["action_size"], saved["config"]["hidden_sizes"])
    model.load_state_dict(initial_weights)
    error, non_greedy = verify_behavior(model, DataLoader(data, BATCH, collate_fn=collate), sampling)
    print(f"Verified {len(data)} sampled choices (max logp error {error:.3g}). "
          f"Non-greedy: {non_greedy}/{len(data)} ({non_greedy / len(data):.2%}).", flush=True)
    device = torch.device("cpu")
    anchor = JsonlPolicyDataset(base / "train.jsonl", spec)
    validation = make_loader(JsonlPolicyDataset(base / "validation.jsonl", spec), 128, False, seed, 0)
    old_validation = run_epoch(model, validation, device)
    optimizer = torch.optim.AdamW(model.parameters(), lr=1e-4, weight_decay=1e-4)
    metrics = []
    for epoch in range(1, EPOCHS + 1):
        behavior = DataLoader(data, BATCH, shuffle=True, generator=torch.Generator().manual_seed(seed + epoch),
                              collate_fn=collate)
        original = make_loader(limit_dataset(anchor, len(data), seed + epoch), BATCH, True, seed + epoch, 0)
        metrics.append(update(model, behavior, original, optimizer, sampling))
        print(json.dumps({"epoch": epoch, **metrics[-1]}), flush=True)
    new_validation = run_epoch(model, validation, device)
    output.mkdir(parents=True)
    torch.save(model.state_dict(), output / "best.pt")
    model_path = write_policy_artifact(model, spec, (6, 4, 3), output)
    fixtures = [parity_fixture(model, spec, r["policy"], r["source"]) for r in read_json(artifact / "parity.json")]
    (output / "parity.json").write_text(json.dumps(fixtures, allow_nan=False), encoding="utf-8")
    (output / "export.json").write_text(json.dumps({
        "artifactVersion": 1, "artifactSha256": sha256(model_path), "checkpointSha256": sha256(output / "best.pt"),
        "dataset": dataset, "epoch": EPOCHS, "baselineSha256": exported["artifactSha256"],
        "featureSpec": spec,
    }, indent=2), encoding="utf-8")
    (output / "manifest.json").write_text(json.dumps({
        "sources": sources, "terminal_outcomes": returns, "observed_choices": len(data), "epochs": metrics,
        "baseline_validation": old_validation, "candidate_validation": new_validation,
        "behavior_max_logp_error": error, "seed": seed, "clip": CLIP, "anchor_weight": ANCHOR_WEIGHT,
        "non_greedy_choices": non_greedy,
        "advantage_estimator": "cross_fitted_gae" if use_critic else "group_mean_terminal_return",
        "critic_mse": critic_mse, "gae_lambda": GAE_LAMBDA if use_critic else None,
        "behavior_temperature": sampling[0], "behavior_uniform_exploration": sampling[1],
        "source_fingerprint": receipt["sourceFingerprint"], "opponent_source_hashes": receipt["opponentSourceHashes"],
    }, indent=2), encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("collection", type=Path)
    parser.add_argument("selection_path", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--round", dest="round_number", type=int, choices=(1, 2, 3), default=1)
    parser.add_argument("--critic", dest="use_critic", action="store_true")
    train(**vars(parser.parse_args()))
