# SQL Server Destination

Audience: application developers who want to write pipeline output or dead-letter records into SQL Server.

## What This Covers

This guide shows the current SQL Server transport shape:

- SQL Server destination writes
- SQL Server dead-letter writes
- consumer-owned schema and table mapping
- custom SQL through Dapper-backed execution

## Current Scope

The first SQL Server transport implementation is destination-only.

It supports:

- `WithSqlServerDestination(...)`
- `WithSqlServerDeadLetterDestination(...)`

It does not currently support a SQL Server source, table queue polling, CDC, or change tracking.

## Package

The SQL Server transport lives in:

- `src/Pipelinez.SqlServer`

Install from NuGet:

```bash
dotnet add package Pipelinez.SqlServer
```

## Minimal Destination Example

```csharp
using Pipelinez.Core;
using Pipelinez.Core.Record;
using Pipelinez.SqlServer;
using Pipelinez.SqlServer.Configuration;
using Pipelinez.SqlServer.Mapping;

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithSqlServerDestination(
        new SqlServerDestinationOptions
        {
            ConnectionString = "Server=localhost;Database=pipelinez;User Id=sa;Password=P@ssw0rd!2026;TrustServerCertificate=True"
        },
        SqlServerTableMap<OrderRecord>.ForTable("app", "processed_orders")
            .Map("order_id", record => record.Id)
            .MapJson("payload", record => record)
            .Map("processed_at_utc", _ => DateTimeOffset.UtcNow))
    .Build();

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }
}
```

## Table Shape

`MapJson(...)` writes serialized JSON text. A typical table shape is:

```sql
CREATE SCHEMA [app];

CREATE TABLE [app].[processed_orders] (
    [order_id] nvarchar(128) NOT NULL,
    [payload] nvarchar(max) NOT NULL CHECK (ISJSON([payload]) = 1),
    [processed_at_utc] datetimeoffset NOT NULL
);
```

## Custom SQL Example

```csharp
var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithSqlServerDestination(
        new SqlServerDestinationOptions
        {
            ConnectionString = connectionString
        },
        record => new SqlServerCommandDefinition(
            """
            INSERT INTO [app].[processed_orders] ([order_id], [payload])
            VALUES (@order_id, @payload);
            """,
            new
            {
                order_id = record.Id,
                payload = JsonSerializer.Serialize(record)
            }))
    .Build();
```

## Dead-Letter Mapping Example

```csharp
var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithSqlServerDestination(
        destinationOptions,
        SqlServerTableMap<OrderRecord>.ForTable("app", "processed_orders")
            .Map("order_id", record => record.Id)
            .MapJson("payload", record => record))
    .WithSqlServerDeadLetterDestination(
        deadLetterOptions,
        SqlServerTableMap<PipelineDeadLetterRecord<OrderRecord>>.ForTable("audit", "order_failures")
            .Map("order_id", deadLetter => deadLetter.Record.Id)
            .Map("fault_component", deadLetter => deadLetter.Fault.ComponentName)
            .MapJson("record_json", deadLetter => deadLetter.Record)
            .Map("dead_lettered_at_utc", deadLetter => deadLetter.DeadLetteredAtUtc))
    .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
    .Build();
```

## Connection Configuration

The SQL Server transport supports:

- `ConnectionString`
- `ConfigureConnectionString`
- `CommandTimeoutSeconds`
- `SerializerOptions`

Use `ConfigureConnectionString` when the application wants to set `Application Name`, encryption, pooling, failover, or trust options through `SqlConnectionStringBuilder`.

## Next Steps

- read [SQL Server Transport](../transports/sql-server.md)
- read [Architecture: SQL Server](../architecture/sql-server.md)
- read [Dead-Lettering](../guides/dead-lettering.md)
