"""Compact shared action scorer for behavior cloning."""
import torch
from torch import nn


class ActionScorer(nn.Module):
    def __init__(self, context_size, action_size, hidden_sizes):
        super().__init__()
        widths = [context_size + action_size, *hidden_sizes, 1]
        layers = []
        for index in range(len(widths) - 1):
            layers.append(nn.Linear(widths[index], widths[index + 1]))
            if index < len(widths) - 2:
                layers.append(nn.ReLU())
        self.network = nn.Sequential(*layers)

    def forward(self, contexts, actions, action_mask):
        expanded = contexts.unsqueeze(1).expand(-1, actions.shape[1], -1)
        logits = self.network(torch.cat((expanded, actions), dim=-1)).squeeze(-1)
        return logits.masked_fill(~action_mask, float("-inf"))
