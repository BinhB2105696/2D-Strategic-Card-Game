using System;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

// Regression tests with the installed NGO reader/writer, including its consumed header.
public static class PvpPacketVerification
{
    [MenuItem("Card Game/Networking/Verify PvP Packet Framing")]
    public static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks++; }
        foreach (int prefix in new[] { 0, 8 })
        {
            using (var writer = new FastBufferWriter(prefix + PvpRoomProtocol.PacketBytes, Allocator.Temp))
            {
                if (prefix != 0) writer.WriteValueSafe(123UL);
                writer.WriteValueSafe(PvpRoomProtocol.Welcome); writer.WriteValueSafe(0);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    if (prefix != 0) reader.ReadValueSafe(out ulong header);
                    Check(PvpWireReader.TryControl(reader, out byte command, out int token) &&
                        command == PvpRoomProtocol.Welcome && token == 0,
                        "Welcome was rejected after NGO consumed its named-message header.");
                }
            }
            string json = "{\"kind\":\"view\",\"name\":\"Người chơi\"}";
            byte[] payload = Encoding.UTF8.GetBytes(json);
            using (var writer = new FastBufferWriter(prefix + payload.Length, Allocator.Temp))
            {
                if (prefix != 0) writer.WriteValueSafe(456UL);
                foreach (byte value in payload) writer.WriteValueSafe(value);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    if (prefix != 0) reader.ReadValueSafe(out ulong header);
                    Check(PvpWireReader.TryPayload(reader, out string decoded) && decoded == json,
                        "JSON/Unicode payload was truncated or read past its end after the NGO header.");
                }
            }
        }
        foreach (int length in new[] { PvpRoomProtocol.PacketBytes - 1, PvpRoomProtocol.PacketBytes + 1 })
        {
            using (var reader = new FastBufferReader(new byte[length], Allocator.Temp))
                Check(!PvpWireReader.TryControl(reader, out _, out _), "Malformed control length accepted.");
        }
        using (var reader = new FastBufferReader(new byte[] { 0xff, 0xff }, Allocator.Temp))
            Check(!PvpWireReader.TryPayload(reader, out _), "Invalid UTF-8 accepted.");
        using (var reader = new FastBufferReader(new byte[PvpRoomProtocol.MaxPayloadBytes + 1], Allocator.Temp))
            Check(!PvpWireReader.TryPayload(reader, out _), "Oversized payload accepted.");
        byte[] maximum = Encoding.UTF8.GetBytes(new string('x', PvpRoomProtocol.MaxPayloadBytes));
        using (var writer = new FastBufferWriter(maximum.Length + 8, Allocator.Temp))
        {
            writer.WriteValueSafe(789UL);
            foreach (byte value in maximum) writer.WriteValueSafe(value);
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                reader.ReadValueSafe(out ulong header);
                Check(PvpWireReader.TryPayload(reader, out string decoded) && decoded.Length == maximum.Length,
                    "Maximum legal payload was rejected because of header bytes.");
            }
        }
        Debug.Log("PvP packet framing PASS: " + checks + " SDK reader/writer checks. Network/device acceptance still required.");
    }
}
