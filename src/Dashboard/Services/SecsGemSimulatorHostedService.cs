using FabBridgeEngine.SecsGem;

namespace Dashboard.Services;

/// <summary>
/// Optionally hosts the passive secs4net equipment simulator in-process (the fake GEM tool),
/// so the full rung-5 path runs in one process for a demo. In a real deployment the equipment
/// is a separate machine and this service is simply not registered.
/// </summary>
public sealed class SecsGemSimulatorHostedService : BackgroundService
{
    private readonly Secs4NetEquipmentSimulator _simulator;
    private readonly ILogger<SecsGemSimulatorHostedService> _logger;

    public SecsGemSimulatorHostedService(
        Secs4NetEquipmentSimulator simulator,
        ILogger<SecsGemSimulatorHostedService> logger)
    {
        _simulator = simulator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SECS/GEM equipment simulator (passive) starting.");
        await _simulator.RunAsync(stoppingToken);
    }
}
