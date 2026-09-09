/*───────────────────────────────────────────────────────────────────────────
  FabBridgeEngine — persistence schema (T-SQL / SQL Server)

  One append-only audit table for every translated state change, plus the
  stored procedure the Dapper sink calls. Using a stored proc (not inline SQL)
  gives us: a cached execution plan (fast, repeated inserts), a stable contract
  the app codes against, and a single place to evolve the write logic.
───────────────────────────────────────────────────────────────────────────*/

IF DB_ID('FabBridge') IS NULL
    CREATE DATABASE FabBridge;
GO

USE FabBridge;
GO

-- Append-only telemetry / audit trail of MES state transitions.
IF OBJECT_ID('dbo.EquipmentTelemetry', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EquipmentTelemetry
    (
        Id             BIGINT           IDENTITY(1,1) NOT NULL,
        EquipmentId    NVARCHAR(50)     NOT NULL,
        State          NVARCHAR(16)     NOT NULL,   -- MES state name (Running, Alarm, ...)
        SourceCeid     INT              NOT NULL,   -- originating CEID (traceability)
        EventTimestamp DATETIMEOFFSET(3) NOT NULL,  -- when the event occurred (from equipment)
        LoggedAt       DATETIMEOFFSET(3) NOT NULL
                        CONSTRAINT DF_EquipmentTelemetry_LoggedAt DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_EquipmentTelemetry PRIMARY KEY CLUSTERED (Id)
    );

    -- Dashboards query "latest state per tool" and "recent history for a tool",
    -- so index by equipment + time descending.
    CREATE NONCLUSTERED INDEX IX_EquipmentTelemetry_Equipment_Time
        ON dbo.EquipmentTelemetry (EquipmentId, EventTimestamp DESC)
        INCLUDE (State, SourceCeid);
END
GO
