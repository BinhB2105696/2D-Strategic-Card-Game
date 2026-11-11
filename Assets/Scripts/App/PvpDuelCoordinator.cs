using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PvpDuelCoordinator : MonoBehaviour
{
    public event Action Changed;
    public PvpMatchView View { get; private set; }
    public string Notice { get; private set; } = "";
    public bool Pending { get; private set; }
    public bool CanLeaveResult => View != null && View.finished &&
        (room == null || !room.Connected || !room.IsHost || resultAcknowledged);
    public bool CanReady => room != null && room.Connected && View != null &&
        !View.started && !View.finished && !View.myReady && !Pending;
    private PvpRoomConnection room;
    private Dictionary<string, CardData> cards;
    private CardDefinition[] definitions;
    private string[] deck;
    private string localDisplayName, fingerprint;
    private PvpDuelAuthority authority;
    private bool hadPeer;
    private bool resultAcknowledged;
    private int hostSeat, localPage, remotePage;
    private float pendingUntil;
    private double receivedAt, nextClock;
    private PvpActionQuery localQuery = new PvpActionQuery(), remoteQuery = new PvpActionQuery();
    private int journalSequence;
    private int localAvatar;
    public readonly List<string> Journal = new List<string>();
    public float RemainingSeconds => View == null || View.seconds == null ? 120 :
        Mathf.Max(0, View.seconds[View.timerActor] - (View.clockStarted && !View.finished ?
            (float)(Time.realtimeSinceStartupAsDouble - receivedAt) : 0));

    public void Initialize(PvpRoomConnection connection, IReadOnlyDictionary<string, CardData> catalog,
        SavedDeck selectedDeck, string displayName, int avatarIndex = 0)
    {
        room = connection;
        cards = catalog.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        definitions = cards.Values.Select(CardDataConverter.ToDefinition).ToArray();
        fingerprint = PvpCatalogFingerprint.Compute(definitions);
        SetLocalDeck(selectedDeck, displayName, avatarIndex);
        room.Changed += ConnectionChanged;
        room.PayloadReceived += Receive;
        ConnectionChanged();
    }

    public void SetLocalDeck(SavedDeck selectedDeck, string displayName, int avatarIndex = 0)
    {
        if (room != null && room.Connected) return;
        deck = selectedDeck.cardIds.ToArray(); localDisplayName = PvpDuelAuthority.CleanName(displayName);
        localAvatar = Mathf.Clamp(avatarIndex, 0, 31);
    }

    public void ClearFinished()
    {
        if (View != null && !View.finished) return;
        View = null; authority = null; Notice = ""; Pending = false;
        Changed?.Invoke();
    }

    private void ConnectionChanged()
    {
        if (room.Connected && !hadPeer)
        {
            hadPeer = true; View = null; Notice = ""; Pending = false; resultAcknowledged = false;
            localPage = remotePage = 0;
            localQuery = new PvpActionQuery(); remoteQuery = new PvpActionQuery();
            Journal.Clear(); journalSequence = 0;
            if (room.IsHost)
            {
                uint seed = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0);
                hostSeat = (int)(seed & 1); // Host is not always the first player.
                authority = new PvpDuelAuthority(definitions, fingerprint, seed, ValidateDeck,
                    () => Time.realtimeSinceStartupAsDouble);
                Publish();
            }
        }
        else if (!room.Connected && hadPeer)
        {
            hadPeer = false; Pending = false;
            bool localOffline = room.LeftLocally || Application.internetReachability == NetworkReachability.NotReachable;
            if (View != null && View.started && !View.finished)
            {
                if (authority != null)
                { authority.Forfeit(localOffline ? hostSeat : 1 - hostSeat, "Disconnected"); View = authority.View(hostSeat); }
                else { View.finished = true; View.outcome = localOffline ? "Loss" : "Win"; View.reason = "Disconnected"; }
                Notice = localOffline ? "You disconnected: defeat." : "Opponent disconnected: victory. The room is closed.";
            }
            authority = null;
            if (View == null || !View.started) Notice = "Room connection ended.";
        }
        Changed?.Invoke();
    }

    private string ValidateDeck(string[] ids)
    {
        return LocalDeckStore.Validate(new SavedDeck { cardIds = ids.ToList() }, cards,
            GameConfig.CreateDefault(), out string error) ? null : error;
    }

    public void Ready() => Submit(new PvpMatchMessage
    { kind = "ready", fingerprint = fingerprint, name = localDisplayName, deck = (string[])deck.Clone(), avatar = localAvatar });
    public void Act(int index) => Submit(new PvpMatchMessage
    { kind = "act", matchId = View?.matchId, revision = View?.revision ?? -1, action = index });
    public void Page(int page) => Submit(new PvpMatchMessage { kind = "page", page = page, query = View?.query });
    public void Filter(PvpActionQuery query) => Submit(new PvpMatchMessage { kind = "page", query = query });
    public void BoardReady() => Submit(new PvpMatchMessage { kind = "board-ready", matchId = View?.matchId });
    public void Surrender() => Submit(new PvpMatchMessage { kind = "surrender", matchId = View?.matchId });

    private void Submit(PvpMatchMessage message)
    {
        if (room == null || !room.Connected || Pending) return;
        Notice = "";
        if (room.IsHost) Handle(message, hostSeat, false);
        else
        {
            Pending = true; pendingUntil = Time.realtimeSinceStartup + 12;
            room.SendPayload(JsonUtility.ToJson(message)); Changed?.Invoke();
        }
    }

    private void Receive(string json)
    {
        try
        {
            PvpMatchMessage message = JsonUtility.FromJson<PvpMatchMessage>(json);
            if (message == null || message.protocol != PvpRoomProtocol.Version) return;
            if (room.IsHost) Handle(message, 1 - hostSeat, true);
            else if (message.kind == "view" && ValidView(message.view) &&
                (View == null || message.view.matchId != View.matchId || message.view.revision >= View.revision))
            {
                View = message.view; Pending = false; Notice = View.notice;
                CaptureView();
                if (View.finished)
                    room.SendPayload(JsonUtility.ToJson(new PvpMatchMessage
                    { kind = "ack-result", matchId = View.matchId, revision = View.revision }));
                Changed?.Invoke();
            }
        }
        catch (ArgumentException) { /* Malformed JSON from the peer is ignored. */ }
        catch (Exception exception) { Fail(exception); }
    }

    private void Handle(PvpMatchMessage message, int actor, bool remote)
    {
        if (authority == null) return;
        try
        {
            string error = null;
            if (authority.Tick())
            { localQuery = new PvpActionQuery(); remoteQuery = new PvpActionQuery(); localPage = remotePage = 0; }
            switch (message.kind)
            {
                case "ack-result":
                    PvpMatchView result = authority.View(hostSeat);
                    if (remote && result.finished && message.matchId == result.matchId && message.revision == result.revision)
                    { resultAcknowledged = true; Changed?.Invoke(); }
                    return;
                case "ready": error = authority.Ready(actor, message.fingerprint, message.name, message.deck, message.avatar); break;
                case "board-ready":
                    if (message.matchId == View.matchId) authority.BoardReady(actor);
                    break;
                case "act":
                    error = message.matchId == View.matchId ? authority.Act(actor, message.revision, message.action) : "Match changed.";
                    localPage = remotePage = 0;
                    localQuery = new PvpActionQuery(); remoteQuery = new PvpActionQuery(); break;
                case "surrender":
                    if (message.matchId == View.matchId) authority.Surrender(actor); else error = "Match changed.";
                    break;
                case "page":
                    if (!ValidQuery(message.query)) { error = "Invalid action choices."; break; }
                    if (message.query != null)
                    { message.query.targets = message.query.targets ?? new long[0]; message.query.tributes = message.query.tributes ?? new long[0]; }
                    if (remote) { remotePage = Math.Max(0, message.page); remoteQuery = message.query ?? new PvpActionQuery(); }
                    else { localPage = Math.Max(0, message.page); localQuery = message.query ?? new PvpActionQuery(); }
                    break;
                default: return;
            }
            Publish(remote ? null : error, remote ? error : null);
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static bool ValidQuery(PvpActionQuery query)
    {
        if (query == null) return true;
        return query.source >= 0 && query.zone >= -1 && query.zone < GameConfig.CreateDefault().MonsterZoneCount &&
            query.amount >= -1 && (string.IsNullOrEmpty(query.type) || Enum.TryParse(query.type, out GameActionType _)) &&
            (string.IsNullOrEmpty(query.position) || Enum.TryParse(query.position, out BattlePosition _)) &&
            (query.targets == null || (query.targets.Length <= 40 && query.targets.All(id => id > 0) && query.targets.Distinct().Count() == query.targets.Length)) &&
            (query.tributes == null || (query.tributes.Length <= 40 && query.tributes.All(id => id > 0) && query.tributes.Distinct().Count() == query.tributes.Length));
    }

    private void Publish(string localError = null, string remoteError = null)
    {
        View = authority.View(hostSeat, localPage, localError, localQuery); Notice = localError ?? "";
        CaptureView();
        room.SendPayload(JsonUtility.ToJson(new PvpMatchMessage
        { kind = "view", view = authority.View(1 - hostSeat, remotePage, remoteError, remoteQuery) }));
        Changed?.Invoke();
    }

    private static bool ValidView(PvpMatchView view)
    {
        if (view == null || view.seat < 0 || view.seat > 1 || view.revision < 0 ||
            string.IsNullOrEmpty(view.matchId) || view.matchId.Length > 64 || view.names == null || view.names.Length != 2 ||
            view.options == null || view.options.Length > PvpDuelAuthority.PageSize ||
            view.players == null || view.players.Length != (view.started ? 2 : 0) ||
            view.seconds == null || view.seconds.Length != 2 || view.timerActor < 0 || view.timerActor > 1 ||
            view.query == null || view.choices == null) return false;
        foreach (PvpPlayerView player in view.players)
            if (player == null || player.hand == null || player.hand.Length > 40 ||
                player.monsters == null || player.monsters.Length != 3 || player.spells == null ||
                player.spells.Length != 3 || player.grave == null || player.grave.Length > 40) return false;
        return true;
    }

    private void Update()
    {
        if (hadPeer && View != null && View.started && !View.finished &&
            Application.internetReachability == NetworkReachability.NotReachable)
        { room.Leave("Your network disconnected."); return; }
        if (authority != null && View != null && View.started && !View.finished)
        {
            try
            {
                bool changed = authority.Tick();
                if (changed) { localQuery = new PvpActionQuery(); remoteQuery = new PvpActionQuery(); localPage = remotePage = 0; }
                if (changed || Time.realtimeSinceStartupAsDouble >= nextClock)
                { nextClock = Time.realtimeSinceStartupAsDouble + 1; Publish(); }
            }
            catch (Exception exception) { Fail(exception); }
        }
        if (!Pending || Time.realtimeSinceStartup < pendingUntil) return;
        Pending = false; Notice = "No reply yet. Check the connection before retrying."; Changed?.Invoke();
    }

    private void CaptureView()
    {
        receivedAt = Time.realtimeSinceStartupAsDouble;
        if (View.log == null) return;
        int start = View.logSequence - View.log.Length;
        for (int index = Math.Max(0, journalSequence - start); index < View.log.Length; index++) Journal.Add(View.log[index]);
        journalSequence = Math.Max(journalSequence, View.logSequence);
        if (Journal.Count > 2000) Journal.RemoveRange(0, Journal.Count - 2000);
    }

    private void OnApplicationQuit()
    {
        if (View == null || !View.started || View.finished) return;
        if (authority != null) { authority.Forfeit(hostSeat, "Left"); View = authority.View(hostSeat); }
        else { View.finished = true; View.outcome = "Loss"; View.reason = "Left"; }
        PvpLocalHistory.Save(View, out _);
    }

    private void Fail(Exception exception)
    {
        Notice = "The online match stopped safely. " + exception.GetType().Name;
        Debug.LogWarning(Notice);
        if (View != null && View.started && !View.finished)
        {
            if (authority != null) { authority.Forfeit(hostSeat, "ClientError"); View = authority.View(hostSeat); }
            else { View.finished = true; View.outcome = "Loss"; View.reason = "ClientError"; }
        }
        room.Leave(Notice);
        Changed?.Invoke();
    }

    private void OnDestroy()
    {
        if (room == null) return;
        room.Changed -= ConnectionChanged; room.PayloadReceived -= Receive;
    }
}
