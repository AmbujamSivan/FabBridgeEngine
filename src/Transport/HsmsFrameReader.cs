using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using FabBridgeEngine.Core.Models;

namespace FabBridgeEngine.Transport;

/// <summary>
/// Reads a sequence of <see cref="HsmsFrame"/>s off a byte <see cref="Stream"/> (e.g. a
/// <c>NetworkStream</c>) and yields the decoded <see cref="SecsEvent"/>s.
///
/// The important detail this class gets right: TCP is a STREAM of bytes, not messages.
/// A single logical frame may span several socket reads, and several frames may arrive in
/// one read. <see cref="Stream.ReadExactlyAsync(Memory{byte}, CancellationToken)"/> loops
/// internally until it has filled the requested buffer, so we always read exactly one length
/// prefix, then exactly that many body bytes — correct regardless of how TCP chunks the data.
/// </summary>
public sealed class HsmsFrameReader
{
    private readonly Stream _stream;

    public HsmsFrameReader(Stream stream) => _stream = stream;

    public async IAsyncEnumerable<SecsEvent> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var lengthBuf = new byte[4];

        while (!cancellationToken.IsCancellationRequested)
        {
            // 1) Read the 4-byte length prefix. EndOfStream => peer closed the connection.
            var got = await ReadExactlyOrEofAsync(lengthBuf, cancellationToken);
            if (!got) yield break;

            var bodyLen = BinaryPrimitives.ReadUInt32BigEndian(lengthBuf);
            if (bodyLen is 0 or > 1_000_000)   // sanity guard against a bad/hostile length
                throw new InvalidDataException($"Implausible frame length: {bodyLen}.");

            // 2) Read exactly bodyLen bytes — however many socket reads that takes.
            var body = new byte[bodyLen];
            await _stream.ReadExactlyAsync(body, cancellationToken);

            // 3) Decode. (Parsing lives in HsmsFrame so it's unit-tested without sockets.)
            yield return HsmsFrame.ParseBody(body);
        }
    }

    /// <summary>ReadExactly, but a clean EOF at a frame boundary returns false instead of throwing.</summary>
    private async ValueTask<bool> ReadExactlyOrEofAsync(byte[] buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await _stream.ReadAsync(buffer.AsMemory(read), ct);
            if (n == 0)
                return read == 0 ? false : throw new EndOfStreamException("Stream ended mid-frame.");
            read += n;
        }
        return true;
    }
}
