/*───────────────────────────────────────────────────────────────────────────
  sp_LogEquipmentTelemetry — insert one translated state change.

  Called by SqlTelemetrySink (Dapper) once per event. Kept deliberately tiny:
  a single INSERT so the write stays sub-millisecond under load.
───────────────────────────────────────────────────────────────────────────*/
USE FabBridge;
GO

CREATE OR ALTER PROCEDURE dbo.sp_LogEquipmentTelemetry
    @EquipmentId    NVARCHAR(50),
    @State          NVARCHAR(16),
    @SourceCeid     INT,
    @EventTimestamp DATETIMEOFFSET(3)
AS
BEGIN
    SET NOCOUNT ON;   -- skip the "rows affected" chatter → less network overhead

    INSERT INTO dbo.EquipmentTelemetry (EquipmentId, State, SourceCeid, EventTimestamp)
    VALUES (@EquipmentId, @State, @SourceCeid, @EventTimestamp);
END
GO
