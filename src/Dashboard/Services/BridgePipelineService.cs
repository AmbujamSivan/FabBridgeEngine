using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Ingestion;
using FabBridgeEngine.Core.Translation;

namespace Dashboard.Services;

/// <summary>
/// Runs the FabBridge pipeline for the lifetime of the web app.
///
/// A <see cref="BackgroundService"/> is ASP.NET Core's built-in way to run long-lived
/// background work alongside the web server. On startup it launches the translation
/// worker (consumer) and the equipment source (producer); both run concurrently,
/// decoupled by the bounded channel, until the app shuts down.
/// </summary>
public sealed class BridgePipelineService : BackgroundService
{
    private readonly SecsEventChannel _channel;
    private readonly TranslationWorker _worker;
    private readonly IEquipmentSource _equipment;
    private readonly ILogger<BridgePipelineService> _logger;

    public BridgePipelineService(
        SecsEventChannel channel,
        TranslationWorker worker,
        IEquipmentSource equipment,
        ILogger<BridgePipelineService> logger)
    {
        _channel = channel;
        _worker = worker;
        _equipment = equipment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FabBridge pipeline starting ({Source}).", _equipment.GetType().Name);

        // Consumer starts first so it's ready before events arrive.
        var workerTask = _worker.RunAsync(stoppingToken);

        try
        {
            await _equipment.RunAsync(stoppingToken);   // producer runs until shutdown
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }

        _channel.Complete();     // no more events → let the worker drain and finish
        await workerTask;

        _logger.LogInformation("FabBridge pipeline stopped.");
    }
}
