using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Models;

namespace FabBridgeEngine.Host;

/// <summary>
/// A trivial <see cref="IStateChangeSink"/> that prints each translated state change to
/// the console — our stand-in for the SQL and SignalR sinks we'll add later. It proves
/// the seam works: swapping output destinations means writing a new sink, nothing more.
/// </summary>
public sealed class ConsoleStateChangeSink : IStateChangeSink
{
    public ValueTask PublishAsync(EquipmentStateChange change, CancellationToken ct = default)
    {
        var color = change.State switch
        {
            EquipmentState.Running     => ConsoleColor.Green,
            EquipmentState.Alarm       => ConsoleColor.Red,
            EquipmentState.Maintenance => ConsoleColor.Yellow,
            EquipmentState.Down        => ConsoleColor.DarkRed,
            _                          => ConsoleColor.Gray,
        };

        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(
            $"{change.Timestamp:HH:mm:ss.fff}  {change.EquipmentId,-10}  " +
            $"{change.State,-12}  (CEID {change.SourceCeid})");
        Console.ForegroundColor = prev;

        return ValueTask.CompletedTask;
    }
}
