using System.Threading.Channels;
using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Models;

namespace FabBridgeEngine.Core.Ingestion;

/// <summary>
/// <see cref="IEventChannel"/> backed by a BOUNDED <see cref="Channel{T}"/>.
///
/// Why bounded (a fixed capacity) rather than unbounded?
/// If the consumer ever falls behind an unbounded channel, events pile up until the
/// process runs out of memory and dies — the worst possible failure for a 24/7 bridge.
/// A bounded channel instead applies BACKPRESSURE: once full, <see cref="WriteAsync"/>
/// asynchronously waits for space, which naturally throttles the producer.
/// </summary>
public sealed class SecsEventChannel : IEventChannel
{
    private readonly Channel<SecsEvent> _channel;

    public SecsEventChannel(int capacity = 10_000)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            // If we ever hit capacity, wait for room rather than drop events —
            // losing an event silently would corrupt the MES audit trail.
            FullMode = BoundedChannelFullMode.Wait,

            // Micro-optimizations enabled by our topology: one receiver writes,
            // one worker reads. Telling the channel this lets it skip locks.
            SingleReader = true,
            SingleWriter = true,
        };

        _channel = Channel.CreateBounded<SecsEvent>(options);
    }

    public ValueTask WriteAsync(SecsEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        return _channel.Writer.WriteAsync(evt, cancellationToken);
    }

    public void Complete() => _channel.Writer.Complete();

    public IAsyncEnumerable<SecsEvent> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
