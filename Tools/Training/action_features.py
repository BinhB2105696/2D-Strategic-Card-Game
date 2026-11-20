"""Encoder v1: public policy input only; one feature vector per legal action."""
import math

ENCODER_VERSION = 1
FUNDAMENTALS_ENCODER_VERSION = 2
BOARD_CONTEXT_SIZE = 54
FUNDAMENTALS_ACTION_SIZE = 4


def mean(vectors, width):
    if not vectors:
        return [0.0] * width
    return [sum(values) / len(vectors) for values in zip(*vectors)]


def board_monster(entity):
    return entity[1] == 2 and entity[3] == 1


def energy_shortfall(entity):
    return max(0, int(round(5 * entity[12])) - int(round(5 * entity[11])))


def can_prepare_attack(entity):
    # Position and remaining attacks are visible prerequisites; the legal list handles other restrictions.
    return board_monster(entity) and entity[6] == 1 and entity[15] > 0


def encode(policy, spec):
    expected_schema = int(spec.get("ai_schema", 2))
    expected_globals = int(spec.get("global_feature_count", 21))
    if policy.get("schema") != expected_schema or len(policy["globalFeatures"]) != expected_globals:
        raise ValueError(f"Expected AI schema {expected_schema} and {expected_globals} global features")
    vocabulary = {card: index + 1 for index, card in enumerate(spec["card_ids"])}
    entity_width = 18 + len(vocabulary) + 1
    version = spec.get("encoder_version", ENCODER_VERSION)
    if version not in (ENCODER_VERSION, FUNDAMENTALS_ENCODER_VERSION) or (
            version == FUNDAMENTALS_ENCODER_VERSION and expected_schema != 3):
        raise ValueError("Unsupported policy encoder version")
    entities = []
    for entity in policy["entities"]:
        if len(entity["features"]) != 18:
            raise ValueError("Expected 18 entity features")
        identity = [0.0] * (len(vocabulary) + 1)
        identity[vocabulary.get(entity["cardId"], 0)] = 1.0
        entities.append([float(value) for value in entity["features"]] + identity)

    def entity_vector(index):
        if index == -1:
            return [0.0] * entity_width
        if not isinstance(index, int) or not 0 <= index < len(entities):
            raise ValueError("Invalid local entity index")
        return entities[index]

    context = [float(value) for value in policy["globalFeatures"]]
    # Pool by public owner/zone, retaining counts and each entity's slot feature.
    for owner in (0, 1):
        for zone in spec["zone_values"]:
            group = [vector for vector in entities if vector[0] == owner and vector[1] == zone]
            context += [len(group) / 10.0] + mean(group, entity_width)
    marks = policy.get("retaliationMarks", [])
    mark_vectors = []
    for mark in marks:
        if mark["source"] < 0 or mark["target"] < 0 or len(mark["features"]) != 2:
            raise ValueError("Invalid public mark")
        mark_vectors.append(entity_vector(mark["source"]) + entity_vector(mark["target"]) + list(mark["features"]))
    context += [len(marks) / 10.0] + mean(mark_vectors, entity_width * 2 + 2)
    if version == FUNDAMENTALS_ENCODER_VERSION:
        board = [entity for entity in entities if entity[1] == 2]
        slots = [0.0] * BOARD_CONTEXT_SIZE
        own_board_count = sum(entity[0] == 0 for entity in board)
        own_ready_count = sum(entity[0] == 0 and can_prepare_attack(entity) and energy_shortfall(entity) == 0
                              for entity in board)
        for entity in board:
            if entity[0] not in (0, 1) or entity[2] not in (0, 1, 2):
                raise ValueError("Unexpected board slot in rules-v6 policy input")
            offset = (int(entity[0]) * 3 + int(entity[2])) * 9
            slots[offset:offset + 9] = [1.0, entity[3], entity[7], entity[9], entity[10],
                                        energy_shortfall(entity) / 5, entity[6], entity[15], entity[5]]
        context += slots

    actions = policy["legalActions"]
    if policy["legalActionMask"] != [1] * len(actions):
        raise ValueError("Expected the filtered legal-candidate mask")
    action_vectors = []
    for action in actions:
        if len(action["features"]) != 4:
            raise ValueError("Expected four action features")
        action_type = action["features"][0]
        if action_type not in spec["action_type_values"]:
            raise ValueError("Unknown action operation; update the encoder contract explicitly")
        action_identity = [float(action_type == value) for value in spec["action_type_values"]]
        targets = action["targets"]
        outgoing = [mark for mark in marks if mark["source"] == action["source"] and mark["target"] in targets]
        incoming = [mark for mark in marks if mark["target"] == action["source"] and mark["source"] in targets]
        relation = []
        for relevant in (outgoing, incoming):
            relation += [sum(mark["features"][0] for mark in relevant),
                         max((mark["features"][1] - context[0] for mark in relevant), default=0.0)]
        vector = [float(value) for value in action["features"]] + action_identity
        vector += entity_vector(action["source"])
        vector += mean([entity_vector(index) for index in targets], entity_width)
        vector += mean([entity_vector(index) for index in action["tributes"]], entity_width)
        vector += relation
        if version == FUNDAMENTALS_ENCODER_VERSION:
            shortfall_after = closes_gap = nominal_board_after = ready_after = 0.0
            if action_type == 5:
                source = entity_vector(action["source"])
                shortfall = energy_shortfall(source)
                remaining = max(0, shortfall - int(round(5 * action["features"][2])))
                shortfall_after = remaining / 5
                closes_gap = float(shortfall > 0 and remaining == 0)
                ready_after = (own_ready_count + int(closes_gap and can_prepare_attack(source))) / 3
            if action_type == 4:
                nominal_board_after = (own_board_count + 1 - len(action["tributes"])) / 3
            vector += [shortfall_after, closes_gap, nominal_board_after, ready_after]
        action_vectors.append(vector)
    if any(not math.isfinite(value) for vector in [context] + action_vectors for value in vector):
        raise ValueError("Non-finite model input")
    return context, action_vectors
