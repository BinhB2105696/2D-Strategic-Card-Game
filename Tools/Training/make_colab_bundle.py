"""Create a public-policy-only Colab bundle without raw traces or hidden hands."""
import argparse
import json
import zipfile
from pathlib import Path


TRAINING_FILES = (
    "action_features.py", "policy_model.py", "train_imitation.py", "evaluate_checkpoint.py",
    "smoke_train_config.json", "requirements-cpu.txt", "COLAB.md",
)
DATASET_FILES = ("train.jsonl", "validation.jsonl", "feature_spec.json", "training_manifest.json")


def build(dataset, output):
    root = Path(__file__).resolve().parent
    dataset, output = Path(dataset).resolve(), Path(output).resolve()
    if output.exists():
        raise ValueError("Bundle already exists; choose a new output path")
    manifest = json.loads((dataset / "training_manifest.json").read_text(encoding="utf-8"))
    if manifest.get("status") != "prepared":
        raise ValueError("Dataset is not prepared")
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "x", compression=zipfile.ZIP_DEFLATED) as archive:
        for name in TRAINING_FILES:
            archive.write(root / name, "training/" + name)
        dataset_files = DATASET_FILES + (("test.jsonl",) if (dataset / "test.jsonl").exists() else ())
        for name in dataset_files:
            archive.write(dataset / name, "dataset/" + name)
    return output


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", required=True)
    parser.add_argument("--output", required=True)
    print(build(**vars(parser.parse_args())))
