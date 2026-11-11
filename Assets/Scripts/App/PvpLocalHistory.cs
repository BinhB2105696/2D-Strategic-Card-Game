using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class PvpHistoryRecord
{
    public string matchId, utc, opponent, outcome, reason;
    public int turns;
}

[Serializable]
public sealed class PvpHistoryData
{
    public int schema = 1, wins, losses, draws, cancelled;
    public List<PvpHistoryRecord> recent = new List<PvpHistoryRecord>();
}

// Device-local, host-reported results. Never updates offline PvE/Auto statistics.
public static class PvpLocalHistory
{
    public static string SavePath => Path.Combine(Application.persistentDataPath, "pvp_history_v1.json");

    public static PvpHistoryData Load()
    {
        if (!File.Exists(SavePath)) return new PvpHistoryData();
        var data = JsonUtility.FromJson<PvpHistoryData>(File.ReadAllText(SavePath));
        if (data == null || data.schema != 1 || data.recent == null ||
            data.wins < 0 || data.losses < 0 || data.draws < 0 || data.cancelled < 0)
            throw new InvalidDataException("PvP history is invalid; the existing file was preserved.");
        return data;
    }

    public static bool Save(PvpMatchView view, out string error)
    {
        error = null;
        if (view == null || !view.started || !view.finished) return true;
        try
        {
            PvpHistoryData data = Load();
            if (data.recent.Any(record => record.matchId == view.matchId)) return true;
            switch (view.outcome)
            {
                case "Win": data.wins++; break;
                case "Loss": data.losses++; break;
                case "Draw": data.draws++; break;
                case "Cancelled": data.cancelled++; break;
                default: throw new InvalidDataException("Unknown PvP outcome.");
            }
            data.recent.Insert(0, new PvpHistoryRecord
            {
                matchId = view.matchId, utc = DateTime.UtcNow.ToString("o"),
                opponent = PvpDuelAuthority.CleanName(view.names[1 - view.seat]),
                outcome = view.outcome, reason = view.reason, turns = view.turn
            });
            if (data.recent.Count > 200) data.recent.RemoveRange(200, data.recent.Count - 200);
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(SavePath + ".tmp", JsonUtility.ToJson(data, true));
            if (File.Exists(SavePath)) File.Replace(SavePath + ".tmp", SavePath, SavePath + ".bak");
            else File.Move(SavePath + ".tmp", SavePath);
            return true;
        }
        catch (Exception exception)
        { error = "PvP result could not be saved: " + exception.GetType().Name; return false; }
    }
}
