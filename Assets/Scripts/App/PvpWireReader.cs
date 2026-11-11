using System.Text;
using Unity.Netcode;

// NGO consumes the named-message header before invoking handlers. Length still
// includes that header; only Length - Position bytes belong to our payload.
public static class PvpWireReader
{
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

    public static bool TryControl(FastBufferReader reader, out byte command, out int token)
    {
        command = 0; token = 0;
        if (reader.Length - reader.Position != PvpRoomProtocol.PacketBytes) return false;
        reader.ReadValueSafe(out command);
        reader.ReadValueSafe(out token);
        return true;
    }

    public static bool TryPayload(FastBufferReader reader, out string payload)
    {
        payload = null;
        int remaining = reader.Length - reader.Position;
        if (remaining < 2 || remaining > PvpRoomProtocol.MaxPayloadBytes) return false;
        byte[] bytes = new byte[remaining];
        for (int index = 0; index < remaining; index++) reader.ReadValueSafe(out bytes[index]);
        try { payload = StrictUtf8.GetString(bytes); return true; }
        catch (DecoderFallbackException) { return false; }
    }
}
