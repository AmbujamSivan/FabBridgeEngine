using FabBridgeEngine.Transport;

namespace Dashboard.Services;

/// <summary>
/// Optionally hosts the TCP equipment simulator (the fake "tool") inside the web app, so the
/// whole rung-3 path runs in one process for a demo. In a real deployment the equipment is a
/// separate machine and this service is simply not registered.
/// </summary>
public sealed class EquipmentSimulatorHostedService : BackgroundService
{
    private readonly TcpEquipmentSimulator _simulator;
    private readonly ILogger<EquipmentSimulatorHostedService> _logger;

    public EquipmentSimulatorHostedService(
        TcpEquipmentSimulator simulator,
        ILogger<EquipmentSimulatorHostedService> logger)
    {
        _simulator = simulator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TCP equipment simulator listening on port {Port}.", _simulator.Port);
        await _simulator.RunAsync(stoppingToken);
    }
}
