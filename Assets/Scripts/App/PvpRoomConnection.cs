using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;

// Room lifecycle and bounded message transport. Does not mutate gameplay or local saves.
public sealed class PvpRoomConnection : MonoBehaviour
{
    public event Action Changed;
    public event Action<string> PayloadReceived;
    public string Message { get; private set; } = "Create a room or enter a room code.";
    public string Code { get; private set; } = "";
    public bool Busy => opening || closing;
    public bool CanOpen => !Busy && network == null;
    public bool Connected => peerReady && network != null && network.IsListening;
    public bool CanPing => Connected && pendingPing == 0;
    public bool IsHost => network != null && network.IsHost;
    public bool LeftLocally { get; private set; }

    private NetworkManager network;
    private Task servicesTask;
    private bool opening, closing, hosting, peerReady;
    private int generation, sequence, pendingPing;
    private ulong peer = ulong.MaxValue;
    private float connectionDeadline, pingDeadline, nextHandshake;
    private double pingStarted;
    private string deferredClose;
    private float payloadWindow;
    private int payloadCount;

    public Task CreateAsync() => OpenAsync(null);
    public Task JoinAsync(string code) => OpenAsync(code ?? "");

    private async Task OpenAsync(string requestedCode)
    {
        if (!CanOpen) return;
        string normalized = "";
        if (requestedCode != null && !PvpRoomProtocol.TryCode(requestedCode, out normalized))
        { SetMessage("Enter the 6-12 letter/digit room code from the host."); return; }
        int operation = ++generation;
        LeftLocally = false;
        opening = true;
        hosting = requestedCode == null;
        SetMessage("Connecting to online services...");
        try
        {
            if (servicesTask == null || servicesTask.IsFaulted || servicesTask.IsCanceled)
                servicesTask = InitializeServicesAsync();
            await servicesTask;
            if (this == null || operation != generation) return;
            if (hosting)
            {
                var allocation = await RelayService.Instance.CreateAllocationAsync(1);
                if (this == null || operation != generation) return;
                string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                if (this == null || operation != generation) return;
                UnityTransport transport = CreateNetwork();
                transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
                if (!network.StartHost()) throw new InvalidOperationException("Could not start the room host.");
                RegisterMessages();
                Code = code;
                SetMessage("Room created. Share the code and keep this app open.");
            }
            else
            {
                var allocation = await RelayService.Instance.JoinAllocationAsync(normalized);
                if (this == null || operation != generation) return;
                UnityTransport transport = CreateNetwork();
                transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
                peer = NetworkManager.ServerClientId;
                if (!network.StartClient()) throw new InvalidOperationException("Could not start the room connection.");
                RegisterMessages();
                Code = normalized;
                connectionDeadline = Time.realtimeSinceStartup + 30;
                SetMessage("Joining room. Waiting for the host...");
            }
        }
        catch (Exception exception)
        {
            if (this == null || operation != generation) return;
            string detail = exception is RequestFailedException request
                ? " Service error " + request.ErrorCode + "." : "";
            Leave("Unable to open room." + detail +
                " Check internet, room code and Authentication/Relay setup in Unity Dashboard.", false);
            Debug.LogWarning("PvP connection test failed: " + exception.GetType().Name + detail);
        }
        finally
        {
            if (this != null && operation == generation) { opening = false; Changed?.Invoke(); }
        }
    }

    private static async Task InitializeServicesAsync()
    {
        var options = new InitializationOptions();
        options.SetProfile(Application.isEditor ? "pvp-editor" : "pvp-player");
        await UnityServices.InitializeAsync(options);
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private UnityTransport CreateNetwork()
    {
        if (NetworkManager.Singleton != null)
            throw new InvalidOperationException("Another NetworkManager is already active.");
        var root = new GameObject("PvP Room Network");
        UnityTransport transport = root.AddComponent<UnityTransport>();
        network = root.AddComponent<NetworkManager>();
        network.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = false,
            ConnectionApproval = true,
            ProtocolVersion = PvpRoomProtocol.Version,
            ConnectionData = PvpRoomProtocol.ConnectionData
        };
        network.ConnectionApprovalCallback += Approve;
        network.OnClientConnectedCallback += OnConnected;
        network.OnClientDisconnectCallback += OnDisconnected;
        network.OnTransportFailure += OnTransportFailure;
        return transport;
    }

    private void Approve(NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        bool localHost = request.ClientNetworkId == NetworkManager.ServerClientId;
        bool available = peer == ulong.MaxValue;
        response.Approved = localHost || (available && PvpRoomProtocol.Accepts(request.Payload));
        response.CreatePlayerObject = false;
        response.Pending = false;
        response.Reason = response.Approved ? "" :
            (available ? "Room-test versions differ." : "This room already has two players.");
        if (!localHost && response.Approved) peer = request.ClientNetworkId;
    }

    private void RegisterMessages()
    {
        network.CustomMessagingManager.RegisterNamedMessageHandler(PvpRoomProtocol.Channel, Receive);
        network.CustomMessagingManager.RegisterNamedMessageHandler(PvpRoomProtocol.MatchChannel, ReceivePayload);
    }

    public void SendPayload(string payload)
    {
        if (!Connected) return;
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        if (bytes.Length > PvpRoomProtocol.MaxPayloadBytes)
            throw new InvalidOperationException("PvP message exceeds its size limit.");
        using (var writer = new FastBufferWriter(bytes.Length, Allocator.Temp))
        {
            foreach (byte value in bytes) writer.WriteValueSafe(value);
            network.CustomMessagingManager.SendNamedMessage(PvpRoomProtocol.MatchChannel, peer, writer,
                NetworkDelivery.ReliableFragmentedSequenced);
        }
    }

    private void ReceivePayload(ulong sender, FastBufferReader reader)
    {
        if (!Connected || sender != peer) return;
        if (Time.realtimeSinceStartup - payloadWindow >= 1)
        { payloadWindow = Time.realtimeSinceStartup; payloadCount = 0; }
        if (++payloadCount > 20) return;
        if (PvpWireReader.TryPayload(reader, out string payload)) PayloadReceived?.Invoke(payload);
    }

    private void OnConnected(ulong client)
    {
        if (closing) return;
        if (hosting)
        {
            if (client == NetworkManager.ServerClientId) return;
            peer = client;
            connectionDeadline = Time.realtimeSinceStartup + 15;
            nextHandshake = Time.realtimeSinceStartup + .75f;
            SetMessage("Second player connected. Checking communication...");
            Send(PvpRoomProtocol.Welcome, 0);
        }
        else
        {
            peer = NetworkManager.ServerClientId;
            SetMessage("Connected to host. Checking communication...");
        }
    }

    private void Receive(ulong sender, FastBufferReader reader)
    {
        if (closing || sender != peer ||
            !PvpWireReader.TryControl(reader, out byte command, out int token)) return;
        if (!hosting && command == PvpRoomProtocol.Welcome && token == 0)
        {
            Send(PvpRoomProtocol.Ready, 0);
            MarkConnected();
        }
        else if (hosting && command == PvpRoomProtocol.Ready && token == 0) MarkConnected();
        else if (Connected && command == PvpRoomProtocol.Ping && token > 0)
        {
            Send(PvpRoomProtocol.Pong, token);
            SetMessage("Received the other player's test signal and replied.");
        }
        else if (Connected && command == PvpRoomProtocol.Pong && token > 0 && token == pendingPing)
        {
            pendingPing = 0;
            SetMessage("Two-way signal verified: " +
                ((Time.realtimeSinceStartupAsDouble - pingStarted) * 1000).ToString("0") + " ms round trip.");
        }
    }

    private void MarkConnected()
    {
        if (peerReady) return;
        peerReady = true;
        connectionDeadline = 0;
        SetMessage("Two players connected. Send a test signal from either device.");
    }

    public void Ping()
    {
        if (!CanPing) return;
        sequence = sequence == int.MaxValue ? 1 : sequence + 1;
        pendingPing = sequence;
        pingStarted = Time.realtimeSinceStartupAsDouble;
        pingDeadline = Time.realtimeSinceStartup + 8;
        Send(PvpRoomProtocol.Ping, pendingPing);
        SetMessage("Test signal sent. Waiting for reply...");
    }

    private void Send(byte command, int token)
    {
        using (var writer = new FastBufferWriter(PvpRoomProtocol.PacketBytes, Allocator.Temp))
        {
            writer.WriteValueSafe(command);
            writer.WriteValueSafe(token);
            network.CustomMessagingManager.SendNamedMessage(PvpRoomProtocol.Channel, peer, writer,
                NetworkDelivery.ReliableSequenced);
        }
    }

    private void Update()
    {
        if (closing) return;
        if (deferredClose != null)
        { string reason = deferredClose; deferredClose = null; Leave(reason, false); return; }
        if (connectionDeadline > 0 && Time.realtimeSinceStartup >= connectionDeadline)
        { Leave("Connection timed out. Ask the host for a new room code and retry.", false); return; }
        if (hosting && !peerReady && connectionDeadline > 0 && network != null &&
            network.IsListening && Time.realtimeSinceStartup >= nextHandshake)
        {
            nextHandshake = Time.realtimeSinceStartup + .75f;
            Send(PvpRoomProtocol.Welcome, 0);
        }
        if (pendingPing > 0 && Time.realtimeSinceStartup >= pingDeadline)
        { pendingPing = 0; SetMessage("Test signal timed out. Check the other device and retry."); }
    }

    private void OnDisconnected(ulong client)
    {
        if (closing) return;
        if (hosting && client != peer) return;
        deferredClose = "Room connection ended. Create or join a new room to retry.";
    }

    private void OnTransportFailure() => deferredClose = "Network transport failed. Leave and retry.";

    public void Leave(string message = "Room closed. You can create or join another room.", bool voluntary = true)
    {
        if (closing) return;
        LeftLocally = voluntary;
        generation++;
        opening = peerReady = false;
        connectionDeadline = 0;
        pendingPing = 0;
        peer = ulong.MaxValue;
        Code = "";
        deferredClose = null;
        if (network != null)
        {
            closing = true;
            Detach();
            network.Shutdown();
            StartCoroutine(FinishClose(message));
            SetMessage("Closing room...");
        }
        else SetMessage(message);
    }

    private IEnumerator FinishClose(string message)
    {
        yield return null;
        float deadline = Time.realtimeSinceStartup + 3;
        while (network != null && network.IsListening && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (network != null) Destroy(network.gameObject);
        network = null;
        yield return null; // Let Unity release the old NetworkManager singleton before retry.
        closing = false;
        SetMessage(message);
    }

    private void Detach()
    {
        network.ConnectionApprovalCallback -= Approve;
        network.OnClientConnectedCallback -= OnConnected;
        network.OnClientDisconnectCallback -= OnDisconnected;
        network.OnTransportFailure -= OnTransportFailure;
        network.CustomMessagingManager?.UnregisterNamedMessageHandler(PvpRoomProtocol.Channel);
        network.CustomMessagingManager?.UnregisterNamedMessageHandler(PvpRoomProtocol.MatchChannel);
    }

    private void OnDestroy()
    {
        generation++;
        if (network == null) return;
        Detach();
        network.Shutdown();
        Destroy(network.gameObject);
    }

    private void SetMessage(string message) { Message = message; Changed?.Invoke(); }
}
