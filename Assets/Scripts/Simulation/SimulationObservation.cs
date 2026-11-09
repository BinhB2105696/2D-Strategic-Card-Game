using System.Collections.Generic;
using System.Linq;

public static class SimulationObservation
{
    // Shared view plus explicitly public/owned data supplied to Normal.Observe.
    // Never include opponent hand identities or either deck order in policy input.
    public static object Capture(GameState state, int player)
    {
        var flags = new List<object>();
        foreach (PlayerState owner in state.Players)
        foreach (CardInstance card in owner.MonsterZones)
            if (card != null && (owner.PlayerIndex == player || !card.IsFaceDown))
                flags.Add(SimulationJson.Object("instance", card.InstanceId, "attacksUsed", card.AttacksUsedThisTurn,
                    "maxAttacks", card.MaxAttacksThisTurn, "summonedThisTurn", card.WasSummonedThisTurn,
                    "changedPosition", card.HasChangedPositionThisTurn));
        var graves = state.Players.Select(owner => owner.Graveyard.Select(card =>
            (object)new BotCardView(card)).ToArray()).ToArray();
        return SimulationJson.Object("schema", 1, "sharedView", BotGameStateView.Create(state, player),
            "publicSupplement", SimulationJson.Object("monsterFlags", flags, "graveyards", graves,
                "pendingAttack", state.ActiveChain?.PendingAttack,
                "chain", state.ActiveChain?.Links.ToArray(), "pendingTrigger", state.PendingTriggeredEffect));
    }
}
