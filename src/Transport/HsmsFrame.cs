using System.Buffers.Binary;
using System.Text;
using FabBridgeEngine.Core.Models;

namespace FabBridgeEngine.Transport;

/// <summary>
/// A deliberately simplified, HSMS-*flavoured* wire codec for a single S6F11 event.
///
/// Real HSMS frames a message as: [4-byte length][10-byte header][SECS-II body]. We keep the
/// crucial idea that makes TCP transports work — a LENGTH PREFIX so the receiver knows where
/// each message ends in the byte stream — and carry just the fields our pipeline needs.
/// (Full SECS-II item encoding + the real GEM handshake are rung 5, not here.)
///
/// Frame on the wire (all integers big-endian / network byte order):
///
///   ┌──────────────┬─────────────────────────── body (Length bytes) ───────────────────────────┐
///   │ UInt32 Length│ byte Stream │ byte Function │ UInt32 Ceid │ Int64 UnixMs │ UInt16 IdLen │ Id│
///   └──────────────┴─────────────┴───────────────┴─────────────┴──────────────┴──────────────┴───┘
///     Length = number of body bytes that follow (= 16 + IdLen)
///
/// Stream/Function are fixed to 6/11 (S6F11 = "Event Report Send"); a frame that isn't S6F11
/// is rejected, mirroring a receiver that only handles event reports.
/// </summary>
public static class HsmsFrame
{
    public const byte Stream = 6;
    public const byte Function = 11;

    private const int BodyPrefixLen = 1 + 1 + 4 + 8 + 2; // stream+function+ceid+unixms+idlen = 16

    /// <summary>Serialize an event into a complete length-prefixed frame.</summary>
    public static byte[] Encode(SecsEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var idBytes = Encoding.UTF8.GetBytes(evt.EquipmentId);
        if (idBytes.Length > ushort.MaxValue)
            throw new ArgumentException("EquipmentId too long to frame.", nameof(evt));

        var bodyLen = BodyPrefixLen + idBytes.Length;
        var frame = new byte[4 + bodyLen];
        var span = frame.AsSpan();

        BinaryPrimitives.WriteUInt32BigEndian(span[..4], (uint)bodyLen);   // length prefix
        var body = span[4..];
        body[0] = Stream;
        body[1] = Function;
        BinaryPrimitives.WriteUInt32BigEndian(body[2..6], evt.Ceid);
        BinaryPrimitives.WriteInt64BigEndian(body[6..14], evt.Timestamp.ToUnixTimeMilliseconds());
        BinaryPrimitives.WriteUInt16BigEndian(body[14..16], (ushort)idBytes.Length);
        idBytes.CopyTo(body[16..]);

        return frame;
    }

    /// <summary>
    /// Parse a frame BODY (the bytes after the length prefix) back into a <see cref="SecsEvent"/>.
    /// Throws <see cref="InvalidDataException"/> if the frame is malformed or not an S6F11.
    /// </summary>
    public static SecsEvent ParseBody(ReadOnlySpan<byte> body)
    {
        if (body.Length < BodyPrefixLen)
            throw new InvalidDataException("Frame body shorter than header.");

        var stream = body[0];
        var function = body[1];
        if (stream != Stream || function != Function)
            throw new InvalidDataException($"Unsupported message S{stream}F{function}; expected S6F11.");

        var ceid = BinaryPrimitives.ReadUInt32BigEndian(body[2..6]);
        var unixMs = BinaryPrimitives.ReadInt64BigEndian(body[6..14]);
        var idLen = BinaryPrimitives.ReadUInt16BigEndian(body[14..16]);

        if (body.Length != BodyPrefixLen + idLen)
            throw new InvalidDataException("Frame length does not match EquipmentId length.");

        var equipmentId = Encoding.UTF8.GetString(body[16..(16 + idLen)]);
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(unixMs);

        return new SecsEvent(equipmentId, ceid, timestamp, new Dictionary<uint, object>());
    }
}
