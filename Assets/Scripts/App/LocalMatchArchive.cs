using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class ArchivedAction
{
    public GameActionType type;
    public int playerIndex;
    public long sourceInstanceId;
    public long[] targets;
    public long[] tributes;
    public int zoneIndex;
    public int amount;
    public BattlePosition position;
}

[Serializable]
public sealed class ArchivedMatch
{
    public int schemaVersion = 1;
    public int rulesVersion;
    public int catalogVersion;
    public int randomVersion;
    public string localPlayerId;
    public string startedUtc;
    public LocalMatchSummary summary;
    public string[] playerDeck;
    public string[] opponentDeck;
    public List<ArchivedAction> actions = new List<ArchivedAction>();
}

public static class LocalMatchArchive
{
    public static bool Save(AppMatchRequest request, GameSession session, CardData[] opponent,
        string outcome, bool usedAuto, out string error)
    {
        error = null;
        LocalPlayerProfile profile = request.profile;
        if (profile.recentMatches.Exists(item => item.matchId == request.matchId)) return true;
        var summary = new LocalMatchSummary
        {
            matchId = request.matchId, finishedUtc = DateTime.UtcNow.ToString("u"),
            deckId = request.deck.deckId, deckName = request.deck.name,
            outcome = outcome, usedAuto = usedAuto, seed = request.seed, turns = session.State.TurnNumber
        };
        var archive = new ArchivedMatch
        {
            rulesVersion = session.Log.Header.RulesVersion,
            catalogVersion = session.Log.Header.CardCatalogVersion,
            randomVersion = session.Log.Header.RandomAlgorithmVersion,
            localPlayerId = profile.localPlayerId, startedUtc = request.startedUtc, summary = summary,
            playerDeck = request.deck.cardIds.ToArray(), opponentDeck = Array.ConvertAll(opponent, c => c.CardId)
        };
        foreach (ActionRecord record in session.Log.Actions)
        {
            GameAction a = record.Action;
            archive.actions.Add(new ArchivedAction
            {
                type = a.Type, playerIndex = a.PlayerIndex, sourceInstanceId = a.SourceInstanceId,
                targets = Copy(a.TargetInstanceIds), tributes = Copy(a.TributeInstanceIds),
                zoneIndex = a.ZoneIndex, amount = a.Amount, position = a.BattlePosition
            });
        }
        try
        {
            string directory = Path.Combine(Application.persistentDataPath, "matches", profile.localPlayerId);
            Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory, request.matchId + ".json");
            string pending = destination + ".pending";
            File.WriteAllText(pending, JsonUtility.ToJson(archive, true));
            if (File.Exists(destination)) File.Replace(pending, destination, destination + ".backup");
            else File.Move(pending, destination);
        }
        catch (Exception exception) { error = "Could not save match log: " + exception.Message; return false; }
        var previous = new List<LocalMatchSummary>(profile.recentMatches);
        int wins = profile.wins, losses = profile.losses;
        profile.recentMatches.Insert(0, summary);
        if (!usedAuto)
        {
            if (outcome == "Win") profile.wins++;
            if (outcome == "Loss") profile.losses++;
        }
        if (AppProfileServices.Profiles.Save(profile, request.catalog, out error)) return true;
        profile.recentMatches = previous; profile.wins = wins; profile.losses = losses;
        return false;
    }

    private static long[] Copy(IReadOnlyList<long> list)
    {
        long[] result = new long[list.Count];
        for (int i = 0; i < result.Length; i++) result[i] = list[i];
        return result;
    }
}
