"""Factual tactical outcomes and lessons from replay-verified rules-v6 games."""
import csv
import json
from collections import Counter, defaultdict
from pathlib import Path

from action_features import can_prepare_attack, energy_shortfall
from train_learned_reward_pilot import collection_schedule, records
from train_imitation import read_json, sha256


def board(policy, owner):
    return [entry["features"] for entry in policy["entities"]
            if entry["features"][:2] == [owner, 2]]


def certain_wins(policy):
    """Sufficient one-action wins with no possible back-row response."""
    state = policy["globalFeatures"]
    entities = [entry["features"] for entry in policy["entities"]]
    if state[1:6] != [3, 1, 1, 0, 0] or min(state[6:8]) <= 0 or any(e[1] == 3 for e in entities):
        return []
    enemy_lp = round(1000 * state[7])
    marks = {(m["source"], m["target"]): round(1000 * m["features"][0]) for m in policy["retaliationMarks"]}
    wins = []
    for i, action in enumerate(policy["legalActions"]):
        if action["features"][0] != 7:
            continue
        source = action["source"]
        attacker = entities[source]
        power = round(1000 * attacker[9])
        if not action["targets"]:
            if power >= enemy_lp:
                wins.append(i)
            continue
        target, = action["targets"]
        defender = entities[target]
        hp = round(1000 * defender[7])
        if (defender[3] != 1 or defender[6] not in (1, 2) or hp <= 0
                or (defender[6] == 2 and round(1000 * attacker[7]) <= hp)):
            continue
        defense = round(1000 * defender[10]) if defender[6] == 2 else 0
        if (max(0, power - defense) - marks.get((target, source), 0) >= hp
                and 500 * (int(defender[5]) + 1) >= enemy_lp):
            wins.append(i)
    return wins


def read_games(collection, policy_hash):
    receipt = read_json(collection)
    report = read_json(receipt["replayReport"])
    run = Path(receipt["runDirectory"]).resolve()
    metadata = read_json(run / "run.json")
    if (report["status"] != "completed" or report["verifiedMatches"] != 192 or len(report["results"]) != 192
            or Path(report["sourceRun"]).resolve() != run or receipt["policySha256"] != policy_hash
            or metadata["learnedPolicySha256"] != policy_hash or not metadata["config"]["lowerLearnerExploration"]
            or metadata["sourceFingerprint"] != receipt["sourceFingerprint"]
            or (metadata["rulesVersion"], metadata["catalogVersion"], metadata["aiContract"]["schema"]) != (6, 4, 3)):
        raise ValueError("Expected the verified round-3 reward collection")
    for path, digest in receipt["opponentSourceHashes"].items():
        if sha256(Path(path)).lower() != digest.lower():
            raise ValueError(f"Opponent source changed: {path}")
    schedule = collection_schedule(metadata["config"], 3)
    for row in report["results"]:
        verification = read_json(row["report"])
        trace = (run / "traces" / row["trace"]).resolve()
        dataset = Path(row["dataset"]).resolve()
        if (trace.parent != run / "traces" or not verification["verified"] or not verification["completeMatch"]
                or verification["truncated"] or Path(verification["trace"]).resolve() != trace
                or Path(verification["dataset"]).resolve() != dataset):
            raise ValueError(f"Unverified trajectory: {trace}")
        exported = records(dataset)
        header = next(exported)
        decisions = {sample["sequence"]: sample for sample in exported if sample["record"] == "transition"}
        trace_rows = records(trace)
        header_trace = next(trace_rows)
        match = header_trace["matchId"]
        seed, side, opponent, deck, _ = schedule.pop(match)
        if (header["schema"] != 3 or header["matchId"] != match or len(decisions) != verification["verifiedDecisions"]
                or header["sourceFingerprint"] != metadata["sourceFingerprint"]
                or header["catalogFingerprint"] != metadata["catalogFingerprint"]
                or Path(header["sourceTrace"]).resolve() != trace or seed != header_trace["seed"]
                or header_trace[f"bot{side}"] != "LearnedTrain" or header_trace[f"bot{1 - side}"] != opponent):
            raise ValueError(f"Incompatible trajectory: {trace}")
        hard_side = 1 - side if opponent == "Hard" else -1
        if hard_side >= 0 and header_trace["botProfiles"][hard_side]["iterations"] != 96:
            raise ValueError("Expected full-strength Hard")
        attempts, outcomes = {}, {}
        for item in trace_rows:
            if item["record"] == "attempt":
                attempts[item["sequence"]] = item
            elif item["record"] == "accepted":
                outcomes[item["sequence"]] = item
            elif item["record"] == "end":
                final = item["result"]
        if final["status"] != "Finished":
            raise ValueError(f"Unfinished match {match}")
        for sequence, decision in decisions.items():
            item = attempts[sequence]
            if (item["automatic"] or item["action"]["player"] != decision["player"]
                    or item["selectedActionIndex"] != decision["selectedActionIndex"]
                    or item["policyInputJson"] != decision["policyInputJson"]):
                raise ValueError(f"Unverified action at {match}:{sequence}")
            item["policy"] = json.loads(decision["policyInputJson"])
            item["result"] = outcomes[sequence]
        yield match, seed, side, opponent, deck, final["winner"], [attempts[i] for i in sorted(decisions)]
    if schedule:
        raise ValueError("Missing scheduled games")


def extract(collection, policy_hash):
    lessons = []
    scores = defaultdict(Counter)
    match_sets = defaultdict(set)
    for match, seed, learner, opponent, deck, winner, steps in read_games(collection, policy_hash):
        main_start = {}
        attacks = defaultdict(set)
        attacked = defaultdict(set)
        summons = Counter()
        for step in steps:
            action, state = step["action"], step["policy"]["globalFeatures"]
            player, turn = action["player"], step["turn"]
            if state[1:6] == [2, 1, 1, 0, 0]:
                main_start.setdefault((player, turn), step)
            if action["type"] == "Attack":
                attacks[player, turn].add(action["source"])
                attacked[1 - player, turn].update(action["targets"])
            if action["type"] == "SummonMonster":
                summons[player, turn] += 1

        for step in steps:
            policy, raw = step["policy"], step["action"]
            player, turn, kind = raw["player"], step["turn"], raw["type"]
            won = winner == player
            own_board = board(policy, 0)
            state = policy["globalFeatures"]
            pick = step["selectedActionIndex"]
            legal = policy["legalActions"]
            following = main_start.get((player, turn + 2))
            confirmed = []

            def score(skill, condition, succeeded):
                if player != learner:
                    return
                for versus in ("All", opponent):
                    key = (versus, skill, condition)
                    scores[key].update({"actions": 1, "known": int(succeeded is not None), "success": int(succeeded is True)})
                    match_sets[key, "games"].add(match)
                    if won:
                        match_sets[key, "wins"].add(match)

            def next_board_success(source=None):
                if following is None:
                    return None if won or winner == -1 else False
                if source is None:
                    return bool(board(following["policy"], 0))
                visible = json.loads(following["observationJson"])["sharedView"]["Players"][player]
                return any(card and card["InstanceId"] == source for card in visible["MonsterZones"])

            if kind == "AllocateEnergy" and legal[pick]["source"] >= 0:
                entity = policy["entities"][legal[pick]["source"]]["features"]
                gap = energy_shortfall(entity)
                closes = 0 < gap <= raw["amount"] and can_prepare_attack(entity)
                took_attack = raw["source"] in attacks[player, turn]
                condition = "completes attacker's energy" if closes else (
                    "partial allocation" if gap > raw["amount"] else "other allocation")
                score("Energy", condition, took_attack)
                if closes and took_attack:
                    confirmed.append("energy")
            if state[1:6] == [2, 1, 1, 0, 0] and kind in ("AdvancePhase", "EnterEndPhase"):
                score("Formation", f"{len(own_board)} monsters leaving Main", next_board_success())
                score("Summons per turn", f"{summons[player, turn]} summons", next_board_success())
                if len(own_board) == 1:
                    position = "Defense" if own_board[0][6] == 2 else "Attack"
                    score("Lone monster", position, next_board_success())
                if any(e["Type"] == "PhaseChanged" and e["Phase"] == "Battle" for e in step["result"]["events"]):
                    ready = any(can_prepare_attack(e) and energy_shortfall(e) == 0 for e in own_board)
                    score("Enter Battle", "energy-ready attacker" if ready else "no energy-ready attacker",
                          bool(attacks[player, turn]))
                    if not ready and any(a["features"][0] == 5 and a["source"] >= 0 and
                                         can_prepare_attack(policy["entities"][a["source"]]["features"]) and
                                         0 < energy_shortfall(policy["entities"][a["source"]]["features"]) <= round(5*a["features"][2])
                                         for a in legal):
                        score("Enter Battle", "not ready, completing allocation was legal", bool(attacks[player, turn]))
            if kind == "SummonMonster":
                card = policy["entities"][legal[pick]["source"]]
                alive = next_board_success(raw["source"])
                rank = int(card["features"][5])
                score("Summon", f"{len(own_board)} monsters before summon", alive)
                score("Summon position", raw["position"], alive)
                if rank > 0:
                    score("Higher-rank summon", card["cardId"], alive)
                    if any(a["features"][0] == 4 and a["source"] >= 0 and
                           0 < policy["entities"][a["source"]]["features"][5] and
                           policy["entities"][a["source"]]["cardId"] != card["cardId"] for a in legal):
                        score("Higher-rank alternative offered", card["cardId"], alive)
                if alive and (not own_board or rank > 0):
                    confirmed.append("summon")
            if kind == "ChangeBattlePosition" and raw["position"] == "Defense":
                alive = next_board_success(raw["source"])
                exposed = raw["source"] in attacked[player, turn + 1]
                score("Switch to Defense", "targeted next enemy turn" if exposed else "not targeted", alive)
                if exposed and alive:
                    confirmed.append("defense")
            if kind == "Attack" and not any(e["features"][1] == 3 for e in policy["entities"]):
                events = step["result"]["events"]
                destroyed = {e["PlayerIndex"] for e in events if e["Type"] == "MonsterDestroyed"}
                if raw["targets"]:
                    clean_ko = (1 - player) in destroyed and player not in destroyed
                    score("Attack", "targeted, empty back rows", clean_ko)
                    if clean_ko:
                        confirmed.append("attack")
                else:
                    damage = any(e["Type"] == "PlayerDamaged" and e["PlayerIndex"] != player and
                                 e["Amount"] > 0 for e in events)
                    score("Attack", "direct, empty back rows", damage)

            certain = certain_wins(policy) if len(legal) > 1 else []
            if certain:
                if pick in certain and not (step["result"]["terminated"] and step["result"][f"reward{player}"] == 1):
                    raise ValueError(f"Immediate win rule differs from replay at {match}:{step['sequence']}")
                score("Immediate win", "certified choice offered", pick in certain)
                lessons.append((seed, deck, "lethal", policy, certain, match, step["sequence"]))
            elif opponent == "Hard" and player != learner and confirmed and len(legal) > 1:
                # These are successful demonstrations, with no label for unplayed outcomes.
                preferred = [pick]
                if kind == "SummonMonster":
                    preferred = [i for i, a in enumerate(legal) if a["features"][0] == 4 and
                                 a["features"][3] == legal[pick]["features"][3] and a["source"] == legal[pick]["source"]
                                 and set(a["tributes"]) == set(legal[pick]["tributes"])]
                lessons.append((seed, deck, confirmed[0], policy, preferred, match, step["sequence"]))
    return scores, match_sets, lessons


def write_scorecard(path, scores, match_sets):
    fields = ["opponent", "skill", "condition", "actions", "known_outcome", "successful", "score_pct", "games", "wins", "win_pct"]
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        for key, counts in sorted(scores.items()):
            games, wins = len(match_sets[key, "games"]), len(match_sets[key, "wins"])
            known, successful = counts["known"], counts["success"]
            writer.writerow(dict(zip(fields, (*key, counts["actions"], known, successful,
                                             round(100 * successful / known, 2) if known else "",
                                             games, wins, round(100 * wins / games, 2)), strict=True)))
