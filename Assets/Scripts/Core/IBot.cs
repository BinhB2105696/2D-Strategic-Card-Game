using System.Collections.Generic;

public interface IBot
{
    string DisplayName { get; }

    GameAction ChooseAction(
        BotGameStateView state,
        IReadOnlyList<GameAction> legalActions);
}
