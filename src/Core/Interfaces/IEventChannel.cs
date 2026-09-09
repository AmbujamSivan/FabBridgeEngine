using FabBridgeEngine.Core.Models;

namespace FabBridgeEngine.Core.Interfaces;

/// <summary>
/// The in-memory producer/consumer pipe between the network receiver (fast, bursty)
/// and the translation worker (steady). Backed by <c>System.Threading.Channels</c>.
///
/// Split into two halves so each side only sees what it needs:
///  • the RECEIVER holds an <see cref="IEventProducer"/> and only writes.
///  • the TRANSLATOR holds an <see cref="IEventConsumer"/> and only reads.
/// This makes misuse (a consumer writing events) impossible by construction.
/// </summary>
public interface IEventChannel : IEventProducer, IEventConsumer
{
}

/// <summary>Write side — owned by whatever ingests events from equipment.</summary>
public interface IEventProducer
{
    /// <summary>
    /// Enqueue an event. Returns a <see cref="ValueTask"/> that only pauses if the
    /// channel is bounded and currently full — that pause IS the backpressure that
    /// protects memory. Rarely blocks in practice.
    /// </summary>
    ValueTask WriteAsync(SecsEvent evt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signal that no further events will be written, so consumers can drain and stop.
    /// </summary>
    void Complete();
}

/// <summary>Read side — owned by the translation worker.</summary>
public interface IEventConsumer
{
    /// <summary>
    /// Asynchronously yields events in FIFO order as they arrive, until the channel is
    /// completed and drained. Consume with <c>await foreach</c>.
    /// </summary>
    IAsyncEnumerable<SecsEvent> ReadAllAsync(CancellationToken cancellationToken = default);
}
