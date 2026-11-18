import tempfile
import unittest
from pathlib import Path

import torch

from policy_model import ActionScorer
from train_imitation import JsonlPolicyDataset, collate_policy_batch


class TrainerFoundationTests(unittest.TestCase):
    def test_model_masks_padding_and_backpropagates(self):
        model = ActionScorer(3, 2, [4])
        contexts = torch.zeros((2, 3))
        actions = torch.zeros((2, 3, 2))
        mask = torch.tensor([[True, True, False], [True, True, True]])
        logits = model(contexts, actions, mask)
        self.assertTrue(torch.isneginf(logits[0, 2]))
        torch.nn.functional.cross_entropy(logits, torch.tensor([0, 1])).backward()
        self.assertTrue(any(parameter.grad is not None for parameter in model.parameters()))

    def test_jsonl_offsets_and_variable_candidate_collation(self):
        spec = {"card_ids": [], "zone_values": [], "action_type_values": [0]}
        policy = {"schema": 2, "globalFeatures": [0.0] * 21, "entities": [],
                  "legalActionMask": [1, 1], "legalActions": [
                      {"features": [0, 0, 0, 0], "source": -1, "targets": [], "tributes": []},
                      {"features": [0, 0, 0, 0], "source": -1, "targets": [], "tributes": []}],
                  "retaliationMarks": []}
        import json
        temp_root = Path(__file__).resolve().parents[2] / "SimulationResults"
        with tempfile.TemporaryDirectory(dir=temp_root) as folder:
            path = Path(folder) / "samples.jsonl"
            sample = {"selected_action_index": 1, "policy": policy}
            path.write_text(json.dumps(sample) + "\n" + json.dumps(sample) + "\n", encoding="utf-8")
            dataset = JsonlPolicyDataset(path, spec)
            try:
                self.assertEqual(2, len(dataset))
                first, second = dataset[0], dataset[1]
                contexts, actions, mask, labels = collate_policy_batch([first, second])
                self.assertEqual((2, len(first[0])), tuple(contexts.shape))
                self.assertEqual((2, 2), tuple(mask.shape))
                self.assertEqual([1, 1], labels.tolist())
            finally:
                dataset.close()


if __name__ == "__main__":
    unittest.main()
