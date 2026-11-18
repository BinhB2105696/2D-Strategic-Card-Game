"""Train a public-information action-ranking MLP from prepared JSONL data."""
import argparse
import hashlib
import json
import random
import time
from pathlib import Path

import torch
from torch.nn import functional as F
from torch.utils.data import DataLoader, Dataset, Subset

from action_features import encode
from policy_model import ActionScorer


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def sha256(path):
    result = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


class JsonlPolicyDataset(Dataset):
    """Random-access JSONL with only byte offsets retained in memory."""
    def __init__(self, path, spec):
        self.path = Path(path)
        self.spec = spec
        self.offsets = []
        self._stream = None
        with self.path.open("rb") as stream:
            while stream.readline():
                self.offsets.append(stream.tell())
        self.offsets.insert(0, 0)
        self.offsets.pop()

    def __len__(self):
        return len(self.offsets)

    def close(self):
        if self._stream is not None:
            self._stream.close()
            self._stream = None

    def __del__(self):
        self.close()

    def __getitem__(self, index):
        if self._stream is None:
            self._stream = self.path.open("rb")
        self._stream.seek(self.offsets[index])
        sample = json.loads(self._stream.readline().decode("utf-8-sig"))
        context, actions = encode(sample["policy"], self.spec)
        chosen = int(sample["selected_action_index"])
        if len(actions) < 2 or not 0 <= chosen < len(actions):
            raise ValueError("Prepared sample has an invalid chosen action")
        return context, actions, chosen


def collate_policy_batch(samples):
    context_size = len(samples[0][0])
    action_size = len(samples[0][1][0])
    max_actions = max(len(sample[1]) for sample in samples)
    contexts = torch.zeros((len(samples), context_size), dtype=torch.float32)
    actions = torch.zeros((len(samples), max_actions, action_size), dtype=torch.float32)
    mask = torch.zeros((len(samples), max_actions), dtype=torch.bool)
    labels = torch.empty(len(samples), dtype=torch.long)
    for row, (context, candidates, chosen) in enumerate(samples):
        count = len(candidates)
        contexts[row] = torch.tensor(context)
        actions[row, :count] = torch.tensor(candidates)
        mask[row, :count] = True
        labels[row] = chosen
    return contexts, actions, mask, labels


def limit_dataset(dataset, maximum, seed):
    if not maximum or len(dataset) <= maximum:
        return dataset
    generator = torch.Generator().manual_seed(seed)
    return Subset(dataset, torch.randperm(len(dataset), generator=generator)[:maximum].tolist())


def make_loader(dataset, batch_size, shuffle, seed, workers):
    generator = torch.Generator().manual_seed(seed)
    return DataLoader(dataset, batch_size=batch_size, shuffle=shuffle, generator=generator,
                      num_workers=workers, collate_fn=collate_policy_batch)


def run_epoch(model, loader, device, optimizer=None):
    training = optimizer is not None
    model.train(training)
    totals = {"loss": 0.0, "correct1": 0, "correct3": 0, "samples": 0}
    with torch.set_grad_enabled(training):
        for contexts, actions, mask, labels in loader:
            contexts, actions = contexts.to(device), actions.to(device)
            mask, labels = mask.to(device), labels.to(device)
            logits = model(contexts, actions, mask)
            loss = F.cross_entropy(logits, labels)
            if training:
                optimizer.zero_grad(set_to_none=True)
                loss.backward()
                optimizer.step()
            count = labels.shape[0]
            totals["loss"] += loss.item() * count
            totals["correct1"] += (logits.argmax(1) == labels).sum().item()
            k = min(3, logits.shape[1])
            totals["correct3"] += (logits.topk(k, dim=1).indices == labels.unsqueeze(1)).any(1).sum().item()
            totals["samples"] += count
    return {"loss": totals["loss"] / totals["samples"],
            "top1": totals["correct1"] / totals["samples"],
            "top3": totals["correct3"] / totals["samples"],
            "samples": totals["samples"]}


def verify_dataset(dataset_dir):
    manifest = read_json(dataset_dir / "training_manifest.json")
    if manifest.get("status") != "prepared":
        raise ValueError("Dataset manifest is not prepared")
    for name, expected in manifest["files"].items():
        if sha256(dataset_dir / name) != expected:
            raise ValueError("Dataset checksum changed: " + name)
    return manifest, read_json(dataset_dir / "feature_spec.json")


def save_checkpoint(path, model, optimizer, epoch, best_loss, config, spec):
    torch.save({"schema": 1, "epoch": epoch, "best_validation_loss": best_loss,
                "model_state": model.state_dict(), "optimizer_state": optimizer.state_dict(),
                "config": config, "feature_spec": spec}, path)


def train(args):
    dataset_dir = Path(args.dataset).resolve()
    output = Path(args.output).resolve()
    config = read_json(args.config)
    manifest, spec = verify_dataset(dataset_dir)
    if output.exists() and not args.resume:
        raise ValueError("Output already exists; choose a new directory or pass --resume")
    output.mkdir(parents=True, exist_ok=True)

    seed = int(config.get("seed", 20261118))
    random.seed(seed)
    torch.manual_seed(seed)
    torch.set_num_threads(int(config["cpu_threads"]))
    if config.get("deterministic", True):
        torch.use_deterministic_algorithms(True)
    requested = config.get("device", "cpu")
    device = torch.device("cuda" if requested == "auto" and torch.cuda.is_available() else requested)

    train_data = limit_dataset(JsonlPolicyDataset(dataset_dir / "train.jsonl", spec), config.get("max_samples"), seed)
    validation_data = JsonlPolicyDataset(dataset_dir / "validation.jsonl", spec)
    loaders = {
        "train": make_loader(train_data, config["batch_size"], True, seed, config.get("data_loader_workers", 0)),
        "validation": make_loader(validation_data, config["batch_size"], False, seed, 0),
    }
    test_path = dataset_dir / "test.jsonl"
    if test_path.exists():
        loaders["test"] = make_loader(JsonlPolicyDataset(test_path, spec), config["batch_size"], False, seed, 0)

    model = ActionScorer(spec["context_size"], spec["action_size"], config["hidden_sizes"]).to(device)
    optimizer = torch.optim.AdamW(model.parameters(), lr=config["learning_rate"],
                                  weight_decay=config.get("weight_decay", 0.0))
    start_epoch, best_loss = 1, float("inf")
    if args.resume:
        checkpoint = torch.load(args.resume, map_location=device, weights_only=False)
        if checkpoint["feature_spec"] != spec or checkpoint["config"]["hidden_sizes"] != config["hidden_sizes"]:
            raise ValueError("Resume checkpoint is incompatible with the dataset/config")
        model.load_state_dict(checkpoint["model_state"])
        optimizer.load_state_dict(checkpoint["optimizer_state"])
        start_epoch, best_loss = checkpoint["epoch"] + 1, checkpoint["best_validation_loss"]

    run_manifest = {"schema": 1, "status": "running", "method": "supervised behavior cloning",
                    "model": "shared-candidate MLP action scorer", "device": str(device),
                    "torch_version": torch.__version__, "seed": seed, "config": config,
                    "dataset_manifest_sha256": sha256(dataset_dir / "training_manifest.json"),
                    "dataset_counts": manifest["counts"], "train_samples_used": len(train_data),
                    "validation_samples": len(validation_data), "has_independent_test": "test" in loaders}
    (output / "run_manifest.json").write_text(json.dumps(run_manifest, indent=2), encoding="utf-8")
    metrics_path = output / "metrics.jsonl"
    started = time.monotonic()
    completed_epoch = start_epoch - 1
    for epoch in range(start_epoch, int(config["epochs"]) + 1):
        metrics = {"epoch": epoch, "train": run_epoch(model, loaders["train"], device, optimizer),
                   "validation": run_epoch(model, loaders["validation"], device)}
        with metrics_path.open("a", encoding="utf-8") as stream:
            stream.write(json.dumps(metrics, separators=(",", ":")) + "\n")
        completed_epoch = epoch
        if metrics["validation"]["loss"] < best_loss:
            best_loss = metrics["validation"]["loss"]
            save_checkpoint(output / "best.pt", model, optimizer, epoch, best_loss, config, spec)
        save_checkpoint(output / "last.pt", model, optimizer, epoch, best_loss, config, spec)
        print(json.dumps(metrics))
        if time.monotonic() - started >= config.get("max_seconds", float("inf")):
            break

    final_metrics = {"best_validation_loss": best_loss, "completed_epoch": completed_epoch}
    if "test" in loaders:
        best = torch.load(output / "best.pt", map_location=device, weights_only=False)
        model.load_state_dict(best["model_state"])
        final_metrics["test"] = run_epoch(model, loaders["test"], device)
    run_manifest.update(status="completed", elapsed_seconds=time.monotonic() - started,
                        final_metrics=final_metrics,
                        cautions=["Validation imitation accuracy is not game win rate.",
                                  "This smoke dataset has no independent test split."] if "test" not in loaders else [])
    (output / "run_manifest.json").write_text(json.dumps(run_manifest, indent=2), encoding="utf-8")
    for loader in loaders.values():
        dataset = loader.dataset
        if hasattr(dataset, "close"):
            dataset.close()
    return run_manifest


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", required=True)
    parser.add_argument("--config", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--resume")
    print(json.dumps(train(parser.parse_args())["final_metrics"], indent=2))
