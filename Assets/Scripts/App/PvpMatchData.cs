using System;

[Serializable]
public sealed class PvpMatchMessage
{
    public int protocol = PvpRoomProtocol.Version;
    public string kind, fingerprint, name, matchId;
    public string[] deck;
    public int revision, action, page, avatar;
    public PvpActionQuery query;
    public PvpMatchView view;
}

[Serializable]
public sealed class PvpMatchView
{
    public string matchId, phase, outcome, reason, notice;
    public int revision, seat, activePlayer, interactionPlayer, turn, optionTotal, optionPage;
    public bool started, finished, myReady, opponentReady, clockStarted, response, trigger;
    public float[] seconds;
    public int timerActor, logSequence, actionSequence, lastActor;
    public string lastAction;
    public long lastSource, lastTarget;
    public PvpActionQuery query;
    public PvpActionChoices choices;
    public string[] names, log;
    public int[] avatars;
    public PvpPlayerView[] players;
    public PvpActionOption[] options;
}

[Serializable]
public sealed class PvpPlayerView
{
    public int health, energy, maxEnergy, deckCount, handCount, graveCount;
    public PvpPublicCard[] hand, monsters, spells, grave;
    public PvpPublicCard field;
}

[Serializable]
public sealed class PvpPublicCard
{
    public string cardId, name, position;
    public long instanceId;
    public bool known, monster, faceDown;
    public int hp, maxHp, atk, def, energy, attackRequirement;
}

[Serializable]
public sealed class PvpActionOption
{
    public int index;
    public string text;
    public string type;
    public long source;
}

[Serializable]
public sealed class PvpActionQuery
{
    public long source;
    public string type, position;
    public int zone = -1, amount = -1;
    public long[] targets = new long[0], tributes = new long[0];
}

[Serializable]
public sealed class PvpActionChoices
{
    public string[] types = new string[0], positions = new string[0], transitions = new string[0];
    public int[] zones = new int[0], amounts = new int[0];
    public long[] sources = new long[0], targets = new long[0], tributes = new long[0];
    public int targetCount, tributeCount;
}
