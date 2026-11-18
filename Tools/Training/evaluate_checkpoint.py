"""Evaluate a saved action-scoring checkpoint without retraining it."""
import argparse
import json
from pathlib import Path

import torch

from policy_model import ActionScorer
from train_imitation import JsonlPolicyDataset, make_loader, run_epoch, verify_dataset


def evaluate(dataset, checkpoint, split, batch_size=64):
    dataset_dir = Path(dataset).resolve()
    split_path = dataset_dir / f"{split}.jsonl"
    if not split_path.exists():
        raise ValueError(f"Dataset has no {split} split")
    _, spec = verify_dataset(dataset_dir)
    saved = torch.load(checkpoint, map_location="cpu", weights_only=False)
    if saved["feature_spec"] != spec:
        raise ValueError("Checkpoint feature spec does not match the dataset")
    model = ActionScorer(spec["context_size"], spec["action_size"],
                         saved["config"]["hidden_sizes"])
    model.load_state_dict(saved["model_state"])
    dataset = JsonlPolicyDataset(split_path, spec)
    try:
        loader = make_loader(dataset, batch_size, False, 0, 0)
        result = run_epoch(model, loader, torch.device("cpu"))
    finally:
        dataset.close()
    return {"split": split, "checkpoint_epoch": saved["epoch"], **result}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", required=True)
    parser.add_argument("--checkpoint", required=True)
    parser.add_argument("--split", choices=("train", "validation", "test"), required=True)
    parser.add_argument("--batch-size", type=int, default=64)
    print(json.dumps(evaluate(**vars(parser.parse_args())), indent=2))
