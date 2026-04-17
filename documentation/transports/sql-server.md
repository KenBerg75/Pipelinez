# SQL Server

Audience: application developers and operators using the SQL Server transport extension.

## What This Covers

- SQL Server destination setup
- SQL Server dead-letter setup
- consumer-owned schema mapping
- custom SQL execution

## Current Transport Status

SQL Server support lives in:

- `src/Pipelinez.SqlServer`

Current scope:

- SQL Server destination writes
- SQL Server dead-letter writes

Current non-goals:

- SQL Server source support
- table-backed queue polling
- CDC or change tracking sources
- migration or schema ownership

## Consumer-Owned Schema

The SQL Server transport does not require a Pipelinez-owned schema.

Consumers choose:

- schema name
- table name
- column names
- whether to use generated insert SQL or custom SQL

That means the transport can write into:

- existing application tables
- integration/outbox tables
- audit tables
- dead-letter review tables

## Direct Table Mapping

```csharp
var destinationMap = SqlServerTableMap<OrderRecord>.ForTable("app", "processed_orders")
    .Map("order_id", record => record.Id)
    .MapJson("payload", record => record)
    .Map("processed_at_utc", _ => DateTimeOffset.UtcNow);

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithSqlServerDestination(destinationOptions, destinationMap)
    .Build();
```

The generated table-map path creates a parameterized `INSERT` statement, bracket-quotes identifiers, serializes JSON mappings as JSON text, and executes the command through Dapper.

## Custom SQL

```csharp
var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithSqlServerDestination(
        destinationOptions,
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

Use custom SQL when the application needs full control over statements, table hints, stored procedure calls, or database-specific expressions.

## Dead-Letter Mapping

Dead-letter support is available through both:

- `SqlServerTableMap<PipelineDeadLetterRecord<T>>`
- `Func<PipelineDeadLetterRecord<T>, SqlServerCommandDefinition>`

Example:

```csharp
var deadLetterMap = SqlServerTableMap<PipelineDeadLetterRecord<OrderRecord>>.ForTable("audit", "order_failures")
    .Map("order_id", deadLetter => deadLetter.Record.Id)
    .Map("fault_component", deadLetter => deadLetter.Fault.ComponentName)
    .MapJson("record_json", deadLetter => deadLetter.Record)
    .MapJson("metadata_json", deadLetter => deadLetter.Metadata)
    .Map("dead_lettered_at_utc", deadLetter => deadLetter.DeadLetteredAtUtc);
```

## Dapper Execution Model

The SQL Server transport uses Dapper internally for execution.

That choice keeps the transport:

- lightweight
- explicit
- parameterized by default
- compatible with consumer-owned SQL

The public API does not expose Dapper types directly. Pipelinez owns the command abstraction and converts it to Dapper internally.

## Connection Options

Shared SQL Server options support:

- `ConnectionString`
- `ConfigureConnectionString`
- `CommandTimeoutSeconds`
- `SerializerOptions`

Connections are opened with `Microsoft.Data.SqlClient`. Consumers can use `ConfigureConnectionString` to set driver-level options such as `Application Name`, encryption, pooling, or trust settings.

## JSON Columns

SQL Server does not have a dedicated `jsonb` column type. Generated `MapJson(...)` mappings serialize values as JSON text and pass them as parameters.

Use `nvarchar(max)` columns, optionally with `CHECK (ISJSON([column_name]) = 1)`, when the target table should enforce JSON validity.

## Related Docs

- [Getting Started: SQL Server Destination](../getting-started/sql-server-destination.md)
- [Architecture: SQL Server](../architecture/sql-server.md)
- [Dead-Lettering](../guides/dead-lettering.md)
