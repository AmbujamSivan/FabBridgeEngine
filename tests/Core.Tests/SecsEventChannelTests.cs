using FabBridgeEngine.Core.Ingestion;
using FabBridgeEngine.Core.Models;
using Xunit;

namespace FabBridgeEngine.Core.Tests;

public class SecsEventChannelTests
{
    [Fact]
    public async Task Events_AreReadBack_InFifoOrder()
    {
        var channel = new SecsEventChannel(capacity: 100);

        // Producer: enqueue 5 events with distinct CEIDs, then signal completion.
        for (uint ceid = 1; ceid <= 5; ceid++)
            await channel.WriteAsync(SecsEvent.Create("ETCH-07", ceid));
        channel.Complete();

        // Consumer: drain the channel.
        var received = new List<uint>();
        await foreach (var evt in channel.ReadAllAsync())
            received.Add(evt.Ceid);

        Assert.Equal(new uint[] { 1, 2, 3, 4, 5 }, received); // order preserved
    }

    [Fact]
    public async Task Producer_And_Consumer_Can_Run_Concurrently()
    {
        var channel = new SecsEventChannel(capacity: 4); // small: forces backpressure
        const int total = 1_000;

        // Consumer task starts reading immediately, in parallel with the producer.
        var consumer = Task.Run(async () =>
        {
            var count = 0;
            await foreach (var _ in channel.ReadAllAsync())
                count++;
            return count;
        });

        // Producer floods 1000 events through a channel that only holds 4 at a time.
        // WriteAsync transparently waits whenever the buffer is full — no events lost.
        for (uint i = 0; i < total; i++)
            await channel.WriteAsync(SecsEvent.Create("ETCH-07", i));
        channel.Complete();

        var consumed = await consumer;
        Assert.Equal(total, consumed); // every event made it through despite tiny buffer
    }
}
