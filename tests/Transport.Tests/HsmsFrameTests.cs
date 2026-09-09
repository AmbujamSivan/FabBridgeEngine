using FabBridgeEngine.Core.Models;
using FabBridgeEngine.Transport;
using Xunit;

namespace FabBridgeEngine.Transport.Tests;

public class HsmsFrameTests
{
    private static SecsEvent Sample(string id = "ETCH-07", uint ceid = 101) =>
        new(id, ceid, DateTimeOffset.FromUnixTimeMilliseconds(1_725_000_000_123),
            new Dictionary<uint, object>());

    [Fact]
    public void Encode_Then_ParseBody_RoundTrips()
    {
        var evt = Sample();
        var frame = HsmsFrame.Encode(evt);

        var back = HsmsFrame.ParseBody(frame.AsSpan(4)); // skip the 4-byte length prefix

        Assert.Equal(evt.EquipmentId, back.EquipmentId);
        Assert.Equal(evt.Ceid, back.Ceid);
        Assert.Equal(evt.Timestamp.ToUnixTimeMilliseconds(), back.Timestamp.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Length_Prefix_Matches_Body_Size()
    {
        var frame = HsmsFrame.Encode(Sample("CVD-03", 201));
        // First 4 bytes are the big-endian body length; total = 4 + body.
        var bodyLen = (frame[0] << 24) | (frame[1] << 16) | (frame[2] << 8) | frame[3];
        Assert.Equal(frame.Length - 4, bodyLen);
    }

    [Fact]
    public void ParseBody_RejectsNonS6F11()
    {
        var frame = HsmsFrame.Encode(Sample());
        var body = frame.AsSpan(4).ToArray();
        body[1] = 13; // mangle Function 11 -> 13
        Assert.Throws<InvalidDataException>(() => HsmsFrame.ParseBody(body));
    }

    [Fact]
    public async Task Reader_Reads_Multiple_Frames_From_Stream()
    {
        var events = new[] { Sample("ETCH-07", 101), Sample("CVD-03", 201), Sample("LITHO-11", 102) };
        using var ms = new MemoryStream();
        foreach (var e in events) { var f = HsmsFrame.Encode(e); ms.Write(f, 0, f.Length); }
        ms.Position = 0;

        var reader = new HsmsFrameReader(ms);
        var read = new List<SecsEvent>();
        await foreach (var e in reader.ReadAllAsync()) read.Add(e);

        Assert.Equal(3, read.Count);
        Assert.Equal(new uint[] { 101, 201, 102 }, read.Select(e => e.Ceid).ToArray());
        Assert.Equal(new[] { "ETCH-07", "CVD-03", "LITHO-11" }, read.Select(e => e.EquipmentId).ToArray());
    }

    [Fact]
    public async Task Reader_Reassembles_Frames_Split_Across_Reads()
    {
        // The real-world hazard: TCP hands you bytes in arbitrary chunks. This stream returns
        // ONE byte per read, forcing maximum fragmentation. The reader must still decode cleanly.
        var events = new[] { Sample("ETCH-07", 101), Sample("CVD-03", 401) };
        using var inner = new MemoryStream();
        foreach (var e in events) { var f = HsmsFrame.Encode(e); inner.Write(f, 0, f.Length); }
        inner.Position = 0;

        var reader = new HsmsFrameReader(new OneBytePerReadStream(inner));
        var read = new List<SecsEvent>();
        await foreach (var e in reader.ReadAllAsync()) read.Add(e);

        Assert.Equal(2, read.Count);
        Assert.Equal(new uint[] { 101, 401 }, read.Select(e => e.Ceid).ToArray());
    }

    [Fact]
    public async Task Reader_Completes_Cleanly_On_Eof_At_Boundary()
    {
        using var empty = new MemoryStream();     // no data at all
        var reader = new HsmsFrameReader(empty);

        var count = 0;
        await foreach (var _ in reader.ReadAllAsync()) count++;

        Assert.Equal(0, count); // no exception, just an empty sequence
    }

    /// <summary>A Stream that yields at most one byte per read — simulates worst-case TCP fragmentation.</summary>
    private sealed class OneBytePerReadStream : Stream
    {
        private readonly Stream _inner;
        public OneBytePerReadStream(Stream inner) => _inner = inner;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (buffer.Length == 0) return 0;
            return await _inner.ReadAsync(buffer[..1], ct);   // hand back a single byte
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            count == 0 ? 0 : _inner.Read(buffer, offset, 1);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    }
}
