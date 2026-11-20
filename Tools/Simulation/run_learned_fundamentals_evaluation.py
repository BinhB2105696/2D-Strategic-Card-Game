"""Run the paired Hard screening batch from CMD with the built Unity runner."""
import argparse
import hashlib
import json
import msvcrt
import os
from pathlib import Path
import subprocess
import time
import _winapi


PROJECT = Path(__file__).resolve().parents[2]
OUTPUT = PROJECT / "SimulationResults/LearnedFundamentals/v1"
FIRST_SEED = 2026112561
SEED_COUNT = 12
GAMES = 4 * SEED_COUNT


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_live_progress(path):
    # Permit Unity to replace the progress file atomically while its old snapshot is open.
    allow_read_write_delete = 0x7
    handle = _winapi.CreateFile(str(path), 0x80000000, allow_read_write_delete, 0, 3, 0x80, 0)
    try:
        descriptor = msvcrt.open_osfhandle(handle, os.O_RDONLY | os.O_BINARY)
    except OSError:
        _winapi.CloseHandle(handle)
        raise
    with os.fdopen(descriptor, encoding="utf-8-sig") as stream:
        return json.load(stream)


def validate(config, progress, policy_hash):
    run = Path(progress["runDirectory"])
    metadata = read(run / "run.json")
    if (progress["state"] != "completed" or progress["completed"] != GAMES
            or metadata["learnedPolicySha256"] != policy_hash
            or (metadata["rulesVersion"], metadata["catalogVersion"], metadata["aiContract"]["schema"]) != (6, 4, 3)
            or any(metadata["config"].get(key) != value for key, value in config.items())):
        raise ValueError(f"Unexpected evaluation metadata in {run}")
    scheduled = {(seed, deck, side) for seed in range(FIRST_SEED, FIRST_SEED + SEED_COUNT)
                 for deck in (0, 1) for side in (0, 1)}
    with (run / "matches.jsonl").open(encoding="utf-8-sig") as stream:
        rows = list(map(json.loads, stream))
    wins = 0
    for row in rows:
        side = row["leg"]
        if (row["status"] != "Finished" or row.get("error") or row["winner"] not in (-1, 0, 1)
                or row["deck0"] != row["deck1"] or row[f"bot{side}"] != "Learned" or row[f"bot{1-side}"] != "Hard"):
            raise ValueError(f"Unexpected evaluation row: {row['matchId']}")
        scheduled.remove((row["seed"], row["deck0"], side))
        wins += row["winner"] == side
    if scheduled:
        raise ValueError(f"Missing evaluation matches in {run}")
    for row in (r for r in rows if r.get("trace")):
        with (run / "traces" / row["trace"]).open(encoding="utf-8-sig") as stream:
            header = json.loads(next(stream))
        profile = header["botProfiles"][1 - row["leg"]]
        if profile["iterations"] != 96 or profile["learnedRootRanking"]:
            raise ValueError(f"Expected standard Hard in {row['matchId']}")
    if sum(bool(row.get("trace")) for row in rows) != config["detailedMatches"]:
        raise ValueError(f"Missing evaluation traces in {run}")
    return {"matches": len(rows), "wins": wins, "runDirectory": str(run), "policySha256": policy_hash,
            "sourceFingerprint": metadata["sourceFingerprint"], "catalogFingerprint": metadata["catalogFingerprint"]}


def evaluate(name, config, policy_hash, runner, output):
    config_path = output / f"{name}.config.json"
    progress_path = output / f"{name}.progress.json"
    log_path = output / f"{name}.log"
    if progress_path.exists():
        progress = read(progress_path)
        if progress["state"] != "completed":
            raise RuntimeError(f"Existing run has state {progress['state']}. Inspect {progress_path} and {log_path}.")
    else:
        print(f"Running {name} ({GAMES} games). Log: {log_path}", flush=True)
        with subprocess.Popen([str(runner), "-batchmode", "-nographics", "-logFile", str(log_path),
                               "--simulation-config", str(config_path), "--simulation-progress", str(progress_path)],
                              cwd=PROJECT) as process:
            last = None
            try:
                while process.poll() is None:
                    try:
                        snapshot = read_live_progress(progress_path)
                    except (FileNotFoundError, PermissionError):
                        snapshot = None
                    if snapshot:
                        current = snapshot.get("completed"), snapshot.get("activeMatch"), snapshot.get("turn")
                        if current != last:
                            print(f"{name}: {current[0]}/{GAMES}  {current[1]}  turn={current[2]}", flush=True)
                            last = current
                    time.sleep(2)
            except KeyboardInterrupt:
                process.terminate()
                raise
            if process.returncode:
                raise RuntimeError(f"Unity runner exited with code {process.returncode}. Inspect {log_path}.")
        progress = read(progress_path)
    result = validate(config, progress, policy_hash)
    (output / f"{name}.result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(f"{name}: {result['wins']}/{GAMES} wins. {result['runDirectory']}", flush=True)
    return result


def make_config(artifact):
    return {"schemaVersion": 1, "projectRoot": str(PROJECT),
            "learnedPolicyPath": artifact.relative_to(PROJECT).as_posix(),
            "firstSeed": FIRST_SEED, "seedCount": SEED_COUNT, "warmupMatches": 0, "maxTurns": 200,
            "maxActions": 10000, "maxMatchSeconds": 180, "decisionWarningMs": 1000, "detailedMatches": 2,
            "matchups": [{"player0Bot": "Learned", "player1Bot": "Hard", "deck0": deck, "deck1": deck,
                          "swapBots": True} for deck in (0, 1)]}


def main(prepare_only, attack_readiness):
    runner = PROJECT / "Build/Simulation/CardGameSimulation.exe"
    if not prepare_only:
        subprocess.run(["git", "diff", "--quiet", "8fdb490", "--", "Assets/Scripts/Bots",
                        "Assets/Scripts/Core/EasyBot.cs"], cwd=PROJECT, check=True)
        if not runner.is_file():
            raise FileNotFoundError("Build the current headless Windows runner in Unity first")
    baseline = PROJECT / "SimulationResults/LearnedReward/v1/candidate"
    output = OUTPUT.parent / "attack-readiness-v1" if attack_readiness else OUTPUT
    name = "attack-readiness" if attack_readiness else "fundamentals"
    models = {name: output / "candidate"}
    reference = read(baseline / "export.json")["artifactSha256"]
    if read(models[name] / "export.json")["baselineSha256"] != reference:
        raise ValueError("Unexpected candidate provenance")
    results = []
    if attack_readiness:
        if not read(output / "report.json")["passed"]:
            raise ValueError("Attack-readiness offline gate did not pass")
        baseline_artifact = baseline / "learned-policy-v1.bin"
        if hashlib.sha256(baseline_artifact.read_bytes()).hexdigest() != reference:
            raise ValueError(f"Model checksum changed: {baseline_artifact}")
        receipt = validate(make_config(baseline_artifact), read(OUTPUT / "reward-v1.progress.json"), reference)
        if receipt != read(OUTPUT / "reward-v1.result.json"):
            raise ValueError("The saved Reward v1 comparison receipt differs from the completed run")
        results.append(receipt)
    else:
        models["reward-v1"] = baseline
    for name, model in models.items():
        artifact = model / "learned-policy-v1.bin"
        digest = hashlib.sha256(artifact.read_bytes()).hexdigest()
        if digest != read(model / "export.json")["artifactSha256"]:
            raise ValueError(f"Model checksum changed: {artifact}")
        config = make_config(artifact)
        (output / f"{name}.config.json").write_text(json.dumps(config, indent=2), encoding="utf-8")
        if not prepare_only:
            results.append(evaluate(name, config, digest, runner, output))
    if prepare_only:
        print(f"Prepared {len(models) * GAMES} new matches: {output}")
    else:
        for key in ("sourceFingerprint", "catalogFingerprint"):
            if results[0][key] != results[1][key]:
                raise ValueError(f"Comparison {key} differs")
        label = "Attack readiness" if attack_readiness else "Fundamentals"
        print(f"{label} Hard screening complete.", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prepare-only", action="store_true")
    parser.add_argument("--attack-readiness", action="store_true")
    main(**vars(parser.parse_args()))
