using System.Collections.Concurrent;
using FabBridgeEngine.Core.Ingestion;
using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Models;
using FabBridgeEngine.Core.Translation;
using Xunit;

namespace FabBridgeEngine.Core.Tests;

public class TranslationWorkerTests
{
    /// <summary>A test double: an in-memory sink that just records what it receives.</summary>
    private sealed class RecordingSink : IStateChangeSink
    {
        public ConcurrentQueue<EquipmentStateChange> Received { get; } = new();

        public ValueTask PublishAsync(EquipmentStateChange change, CancellationToken ct = default)
        {
            Received.Enqueue(change);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A sink that always throws — proves one bad sink can't break the pipeline.</summary>
    private sealed class FaultySink : IStateChangeSink
    {
        public ValueTask PublishAsync(EquipmentStateChange change, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated DB outage");
    }

    [Fact]
    public async Task Worker_Translates_And_Publishes_Every_Event()
    {
        var channel = new SecsEventChannel();
        var sink = new RecordingSink();
        var worker = new TranslationWorker(
            channel, CeidTranslator.CreateDefault(), new[] { sink });

        // Start the worker consuming in the background.
        var running = worker.RunAsync();

        // Feed a realistic sequence: start → alarm → clear → complete.
        await channel.WriteAsync(SecsEvent.Create("ETCH-07", 101)); // Running
        await channel.WriteAsync(SecsEvent.Create("ETCH-07", 201)); // Alarm
        await channel.WriteAsync(SecsEvent.Create("ETCH-07", 202)); // Idle
        await channel.WriteAsync(SecsEvent.Create("ETCH-07", 102)); // Idle
        channel.Complete();

        await running; // worker exits once the channel drains

        var states = sink.Received.Select(c => c.State).ToArray();
        Assert.Equal(
            new[] { EquipmentState.Running, EquipmentState.Alarm,
                    EquipmentState.Idle, EquipmentState.Idle },
            states);
    }

    [Fact]
    public async Task One_Faulty_Sink_Does_Not_Stop_Other_Sinks()
    {
        var channel = new SecsEventChannel();
        var good = new RecordingSink();
        var worker = new TranslationWorker(
            channel, CeidTranslator.CreateDefault(),
            new IStateChangeSink[] { new FaultySink(), good }); // faulty listed first

        var running = worker.RunAsync();
        await channel.WriteAsync(SecsEvent.Create("ETCH-07", 101));
        channel.Complete();
        await running;

        // Despite the faulty sink throwing, the good sink still received the change.
        Assert.Single(good.Received);
        Assert.Equal(EquipmentState.Running, good.Received.First().State);
    }
}
