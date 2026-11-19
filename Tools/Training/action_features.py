"""Encoder v1: public policy input only; one feature vector per legal action."""
import math

ENCODER_VERSION = 1


def mean(vectors, width):
    if not vectors:
        return [0.0] * width
    return [sum(values) / len(vectors) for values in zip(*vectors)]


def encode(policy, spec):
    expected_schema = int(spec.get("ai_schema", 2))
    expected_globals = int(spec.get("global_feature_count", 21))
    if policy.get("schema") != expected_schema or len(policy["globalFeatures"]) != expected_globals:
        raise ValueError(f"Expected AI schema {expected_schema} and {expected_globals} global features")
    vocabulary = {card: index + 1 for index, card in enumerate(spec["card_ids"])}
    entity_width = 18 + len(vocabulary) + 1
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
        action_vectors.append(vector + relation)
    if any(not math.isfinite(value) for vector in [context] + action_vectors for value in vector):
        raise ValueError("Non-finite model input")
    return context, action_vectors
