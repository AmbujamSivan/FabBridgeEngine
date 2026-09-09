using System.Data;
using Dapper;
using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Models;
using Microsoft.Data.SqlClient;

namespace FabBridgeEngine.Persistence;

/// <summary>
/// Persists every translated <see cref="EquipmentStateChange"/> to SQL Server by calling
/// the <c>sp_LogEquipmentTelemetry</c> stored procedure via Dapper.
///
/// Notes on the design choices:
///  • A NEW SqlConnection per publish looks wasteful but is idiomatic: Microsoft.Data.SqlClient
///    maintains an internal CONNECTION POOL, so open/close just rents/returns a live connection.
///  • CommandType.StoredProcedure → we invoke the proc by name; its cached plan keeps the
///    insert fast under sustained load.
///  • Dapper maps our anonymous object straight onto the proc's parameters — no ADO.NET boilerplate.
/// </summary>
public sealed class SqlTelemetrySink : IStateChangeSink
{
    private readonly string _connectionString;

    public SqlTelemetrySink(string connectionString)
    {
        _connectionString = connectionString
            ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async ValueTask PublishAsync(EquipmentStateChange change, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);

        await conn.ExecuteAsync(new CommandDefinition(
            commandText: "dbo.sp_LogEquipmentTelemetry",
            parameters: new
            {
                EquipmentId    = change.EquipmentId,
                State          = change.State.ToString(),
                SourceCeid     = (int)change.SourceCeid,
                EventTimestamp = change.Timestamp,
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct));
    }
}
