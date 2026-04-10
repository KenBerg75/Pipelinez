# PostgreSQL Destination

Audience: application developers who want to write pipeline output or dead-letter records into PostgreSQL.

## What This Covers

This guide shows the shape of the PostgreSQL transport as it exists today:

- PostgreSQL destination writes
- PostgreSQL dead-letter writes
- consumer-owned schema and table mapping
- custom SQL through Dapper-backed execution

## Current Scope

The first PostgreSQL transport implementation is destination-only.

It supports:

- `WithPostgreSqlDestination(...)`
- `WithPostgreSqlDeadLetterDestination(...)`

It does not currently support a PostgreSQL source.

## Package

The PostgreSQL transport lives in:

- `src/Pipelinez.PostgreSql`

If you are working from source, reference that project directly or pack it locally with the repository packaging workflow.

## Minimal Destination Example

```csharp
using Pipelinez.Core;
using Pipelinez.Core.Record;
using Pipelinez.PostgreSql;
using Pipelinez.PostgreSql.Configuration;
using Pipelinez.PostgreSql.Mapping;

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithPostgreSqlDestination(
        new PostgreSqlDestinationOptions
        {
            ConnectionString = "Host=localhost;Database=pipelinez;Username=postgres;Password=postgres"
        },
        PostgreSqlTableMap<OrderRecord>.ForTable("app", "processed_orders")
            .Map("order_id", record => record.Id)
            .MapJson("payload", record => record)
            .Map("processed_at_utc", _ => DateTimeOffset.UtcNow))
    .Build();

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }
}
```

## Custom SQL Example

```csharp
var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithPostgreSqlDestination(
        new PostgreSqlDestinationOptions
        {
            ConnectionString = "Host=localhost;Database=pipelinez;Username=postgres;Password=postgres"
        },
        record => new PostgreSqlCommandDefinition(
            """
            INSERT INTO app.processed_orders (order_id, payload)
            VALUES (@order_id, @payload::jsonb)
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
    .WithPostgreSqlDestination(
        destinationOptions,
        PostgreSqlTableMap<OrderRecord>.ForTable("app", "processed_orders")
            .Map("order_id", record => record.Id)
            .MapJson("payload", record => record))
    .WithPostgreSqlDeadLetterDestination(
        deadLetterOptions,
        PostgreSqlTableMap<PipelineDeadLetterRecord<OrderRecord>>.ForTable("audit", "order_failures")
            .Map("order_id", deadLetter => deadLetter.Record.Id)
            .Map("fault_component", deadLetter => deadLetter.Fault.ComponentName)
            .MapJson("record_json", deadLetter => deadLetter.Record)
            .Map("dead_lettered_at_utc", deadLetter => deadLetter.DeadLetteredAtUtc))
    .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
    .Build();
```

## Connection Configuration

The PostgreSQL transport supports:

- `ConnectionString`
- `ConfigureConnectionString`
- `ConfigureDataSource`
- externally supplied `NpgsqlDataSource`

That gives consumers full control over pooling, timeouts, type mappings, and driver configuration without forcing Pipelinez to own connection setup.

## Next Steps

- read [PostgreSQL Transport](../transports/postgresql.md)
- read [Architecture: PostgreSQL](../architecture/postgresql.md)
- read [Dead-Lettering](../guides/dead-lettering.md)
