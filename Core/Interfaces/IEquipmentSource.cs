namespace FabBridgeEngine.Core.Interfaces;

/// <summary>
/// A source of equipment events. It is handed the channel's write side
/// (<see cref="IEventProducer"/>) at construction and, when run, pumps
/// <see cref="Models.SecsEvent"/>s into it until cancelled.
///
/// This is the SWAP SEAM for producer fidelity (the "rung ladder"):
///   • rung 1: SimulatedEquipment      — generates events in-process
///   • rung 3: TcpHsmsSource           — reads framed bytes off a TCP socket
///   • rung 5: Secs4NetSource          — a real SECS/GEM host stack
/// Every rung ends the same way — <c>producer.WriteAsync(evt)</c> — so the entire
/// downstream pipeline is identical regardless of where events actually come from.
/// </summary>
public interface IEquipmentSource
{
    /// <summary>Run until <paramref name="cancellationToken"/> is signalled.</summary>
    Task RunAsync(CancellationToken cancellationToken = default);
}
