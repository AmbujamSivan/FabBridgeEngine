using FabBridgeEngine.Core.Ingestion;
using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Simulation;
using FabBridgeEngine.Core.Translation;
using FabBridgeEngine.Host;

// ── Wire the pipeline by hand (we'll switch to DI when we add the web host) ─────────────
//
//   SimulatedEquipment ─writes→ SecsEventChannel ─reads→ TranslationWorker
//                                                              │ translate
//                                                              └─fan out→ IStateChangeSink(s)
//
// Every arrow is an interface, so any box is swappable in isolation.

var channel   = new SecsEventChannel(capacity: 10_000);
var translator = CeidTranslator.CreateDefault();
var sinks     = new IStateChangeSink[] { new ConsoleStateChangeSink() };

var worker = new TranslationWorker(channel, translator, sinks);
IEquipmentSource equipment = new SimulatedEquipment(channel);   // rung 1 (swap for TCP later)

// Ctrl+C → graceful shutdown.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine("FabBridgeEngine — simulated equipment running. Press Ctrl+C to stop.\n");
Console.WriteLine($"{"time",-12}  {"equipment",-10}  {"state",-12}  ceid");
Console.WriteLine(new string('─', 52));

// Consumer (worker) and producer (equipment) run concurrently, decoupled by the channel.
var workerTask = worker.RunAsync(cts.Token);

try
{
    await equipment.RunAsync(cts.Token);   // runs until Ctrl+C
}
catch (OperationCanceledException) { /* expected on shutdown */ }

// Producer stopped → tell the channel no more events are coming, then let the worker drain.
channel.Complete();
await workerTask;

Console.WriteLine("\nShutdown complete.");
