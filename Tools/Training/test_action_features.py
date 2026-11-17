import copy
import unittest

from action_features import encode


class ActionFeaturesTests(unittest.TestCase):
    def setUp(self):
        self.spec = {"card_ids": ["a", "b"], "zone_values": [0, 1], "action_type_values": [0, 1]}
        first, second = [0.0] * 18, [0.0] * 18
        first[:4], second[:4] = [0, 1, 0, 1], [1, 1, 0, 1]
        self.policy = {"schema": 2, "globalFeatures": [0.01] + [0.0] * 20,
                       "entities": [{"cardId": "a", "features": first}, {"cardId": "b", "features": second}],
                       "legalActions": [{"features": [0, 0, 0, 0], "source": 0, "targets": [1], "tributes": []},
                                        {"features": [1, 0, 0, 0], "source": -1, "targets": [], "tributes": []}],
                       "legalActionMask": [1, 1],
                       "retaliationMarks": [{"source": 0, "target": 1, "features": [0.3, 0.02]}]}

    def test_candidate_permutation_preserves_scores_input(self):
        context, actions = encode(self.policy, self.spec)
        changed = copy.deepcopy(self.policy)
        changed["legalActions"].reverse()
        other_context, other_actions = encode(changed, self.spec)
        self.assertEqual(context, other_context)
        self.assertEqual(actions, list(reversed(other_actions)))

    def test_entity_reindex_preserves_semantics(self):
        expected = encode(self.policy, self.spec)
        changed = copy.deepcopy(self.policy)
        changed["entities"].reverse()
        changed["legalActions"][0].update(source=1, targets=[0])
        changed["retaliationMarks"][0].update(source=1, target=0)
        self.assertEqual(expected, encode(changed, self.spec))

    def test_unknown_card_keeps_dimensions(self):
        context, actions = encode(self.policy, self.spec)
        changed = copy.deepcopy(self.policy)
        changed["entities"][0]["cardId"] = "future_card"
        next_context, next_actions = encode(changed, self.spec)
        self.assertEqual(len(context), len(next_context))
        self.assertEqual(len(actions[0]), len(next_actions[0]))

    def test_invalid_reference_rejected(self):
        self.policy["legalActions"][0]["source"] = 999
        with self.assertRaises(ValueError):
            encode(self.policy, self.spec)

    def test_changed_schema_rejected(self):
        self.policy["schema"] = 1
        with self.assertRaises(ValueError):
            encode(self.policy, self.spec)

    def test_no_game_metadata_interface(self):
        expected = encode(self.policy, self.spec)
        self.policy["diagnostic_only"] = {"seed": 123, "deck": ["private"]}
        self.assertEqual(expected, encode(self.policy, self.spec))


if __name__ == "__main__":
    unittest.main()
