# FabBridge database (SQL Server via Docker)

Local dev uses SQL Server 2022 in a container. The SA password below is a **local
throwaway** — in any real deployment, supply the connection string via the
`FABBRIDGE_SQL` environment variable and use a secret store, not source control.

## Start the container (one time)

```bash
docker run -d --name fabbridge-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=FabBridge!2026" \
  -p 1433:1433 \
  --platform linux/amd64 \
  mcr.microsoft.com/mssql/server:2022-latest
```

(`--platform linux/amd64` runs the amd64 image under emulation on Apple Silicon.)

## Apply schema + stored procedure

```bash
docker cp db/01_schema.sql fabbridge-sql:/tmp/01_schema.sql
docker cp db/02_sp_LogEquipmentTelemetry.sql fabbridge-sql:/tmp/02_sp.sql
docker exec fabbridge-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'FabBridge!2026' -C -No -i /tmp/01_schema.sql
docker exec fabbridge-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'FabBridge!2026' -C -No -i /tmp/02_sp.sql
```

## Everyday container control

```bash
docker start fabbridge-sql   # resume after a reboot (data persists in the container)
docker stop  fabbridge-sql
```

## Peek at telemetry

```bash
docker exec fabbridge-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'FabBridge!2026' -C -No -d FabBridge -W -s "|" \
  -Q "SELECT TOP 10 EquipmentId, State, SourceCeid, EventTimestamp
      FROM dbo.EquipmentTelemetry ORDER BY Id DESC;"
```
