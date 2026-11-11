using System.Text;

// Versioned room envelope and bounded match channel. Never transmit seeds/account tokens.
public static class PvpRoomProtocol
{
    public const ushort Version = 4;
    public const string Channel = "card.room.v4";
    public const string MatchChannel = "card.duel.v4";
    public const int MaxPayloadBytes = 32768;
    public const byte Welcome = 1, Ready = 2, Ping = 3, Pong = 4;
    public const int PacketBytes = sizeof(byte) + sizeof(int);
    public const int MaxCodeLength = 12;
    private const string Handshake = "card-room/4";

    public static byte[] ConnectionData => Encoding.ASCII.GetBytes(Handshake);

    public static bool Accepts(byte[] payload) => payload != null &&
        payload.Length == Handshake.Length && Encoding.ASCII.GetString(payload) == Handshake;

    public static bool TryCode(string input, out string code)
    {
        code = (input ?? "").Trim().ToUpperInvariant();
        if (code.Length < 6 || code.Length > MaxCodeLength) return false;
        foreach (char character in code)
            if (!(character >= 'A' && character <= 'Z') &&
                !(character >= '0' && character <= '9')) return false;
        return true;
    }
}
