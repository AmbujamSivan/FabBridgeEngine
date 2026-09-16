using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Models;

namespace FabBridgeEngine.Core.Simulation;

/// <summary>
/// Rung-1 producer: an in-process fake fab tool. Each configured equipment id runs its
/// own independent lifecycle loop (start → process → complete, with occasional alarms and
/// maintenance), emitting the same CEIDs a real tool would via <c>S6F11</c>.
///
/// Timing is jittered so the stream looks realistic and the several tools interleave —
/// exactly the bursty, out-of-lockstep pattern the bounded channel is designed to absorb.
/// </summary>
public sealed class SimulatedEquipment : IEquipmentSource
{
    private readonly IEventProducer _producer;
    private readonly IReadOnlyList<string> _equipmentIds;
    private readonly int? _seed;

    public SimulatedEquipment(
        IEventProducer producer,
        IEnumerable<string>? equipmentIds = null,
        int? seed = null)
    {
        _producer = producer;
        _equipmentIds = (equipmentIds ?? new[] { "ETCH-07", "CVD-03", "LITHO-11" }).ToArray();
        _seed = seed;
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        // One concurrent loop per tool; they interleave into the single channel.
        // Each loop gets its OWN Random — System.Random is not thread-safe, and these loops
        // run concurrently (Task.WhenAll), so a shared instance would be a data race. A per-tool
        // seed (base + index) keeps runs reproducible when a seed is supplied for tests.
        var loops = _equipmentIds.Select((id, index) =>
        {
            var rng = _seed is { } s ? new Random(s + index) : new Random();
            return RunToolLoopAsync(id, rng, cancellationToken);
        });
        return Task.WhenAll(loops);
    }

    private async Task RunToolLoopAsync(string equipmentId, Random rng, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Begin a processing run.
                await Emit(equipmentId, ceid: 101, ct);            // Running
                await Jitter(rng, 800, 2500, ct);

                // ~15% of runs raise (and then clear) an alarm mid-process.
                if (rng.NextDouble() < 0.15)
                {
                    await Emit(equipmentId, ceid: 201, ct);        // Alarm
                    await Jitter(rng, 500, 1500, ct);
                    await Emit(equipmentId, ceid: 202, ct);        // cleared → Idle
                    await Jitter(rng, 300, 900, ct);
                }

                await Emit(equipmentId, ceid: 102, ct);            // process complete → Idle
                await Jitter(rng, 600, 2000, ct);

                // ~8% chance the tool goes into a short maintenance window.
                if (rng.NextDouble() < 0.08)
                {
                    await Emit(equipmentId, ceid: 301, ct);        // Maintenance
                    await Jitter(rng, 1500, 4000, ct);
                    await Emit(equipmentId, ceid: 302, ct);        // done → Idle
                    await Jitter(rng, 500, 1200, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — swallow.
        }
    }

    private ValueTask Emit(string equipmentId, uint ceid, CancellationToken ct) =>
        _producer.WriteAsync(SecsEvent.Create(equipmentId, ceid), ct);

    private static Task Jitter(Random rng, int minMs, int maxMs, CancellationToken ct) =>
        Task.Delay(rng.Next(minMs, maxMs), ct);
}
