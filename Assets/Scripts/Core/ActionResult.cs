using System;
using System.Collections.Generic;

[Serializable]
public sealed class ActionResult
{
    private static readonly GameEvent[] NoEvents = new GameEvent[0];

    public bool Succeeded { get; }
    public string ErrorMessage { get; }
    public IReadOnlyList<GameEvent> Events { get; }

    private ActionResult(
        bool succeeded,
        string errorMessage,
        IReadOnlyList<GameEvent> events)
    {
        Succeeded = succeeded;
        ErrorMessage = errorMessage ?? string.Empty;
        Events = events ?? NoEvents;
    }

    public static ActionResult Success(IReadOnlyList<GameEvent> events)
    {
        return new ActionResult(true, string.Empty, events);
    }

    public static ActionResult Failure(string errorMessage)
    {
        return new ActionResult(false, errorMessage, NoEvents);
    }
}
