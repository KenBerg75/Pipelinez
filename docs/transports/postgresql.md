# PostgreSQL

Audience: application developers and operators using the PostgreSQL transport extension.

## What This Covers

- PostgreSQL destination setup
- PostgreSQL dead-letter setup
- consumer-owned schema mapping
- custom SQL execution

## Current Transport Status

PostgreSQL support lives in:

- `src/Pipelinez.PostgreSql`

Current scope:

- PostgreSQL destination writes
- PostgreSQL dead-letter writes

Current non-goal:

- PostgreSQL source support

## Consumer-Owned Schema

The PostgreSQL transport does not require a Pipelinez-owned schema.

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
var destinationMap = PostgreSqlTableMap<OrderRecord>.ForTable("app", "processed_orders")
    .Map("order_id", record => record.Id)
    .MapJson("payload", record => record)
    .Map("processed_at_utc", _ => DateTimeOffset.UtcNow);

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithPostgreSqlDestination(destinationOptions, destinationMap)
    .Build();
```

The generated table-map path creates a parameterized `INSERT` statement and executes it through Dapper.

## Custom SQL

```csharp
var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithPostgreSqlDestination(
        destinationOptions,
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

This is the path to use when the consumer wants to own the SQL completely.

## Dead-Letter Mapping

Dead-letter support is available through both:

- `PostgreSqlTableMap<PipelineDeadLetterRecord<T>>`
- `Func<PipelineDeadLetterRecord<T>, PostgreSqlCommandDefinition>`

Example:

```csharp
var deadLetterMap = PostgreSqlTableMap<PipelineDeadLetterRecord<OrderRecord>>.ForTable("audit", "order_failures")
    .Map("order_id", deadLetter => deadLetter.Record.Id)
    .Map("fault_component", deadLetter => deadLetter.Fault.ComponentName)
    .MapJson("record_json", deadLetter => deadLetter.Record)
    .MapJson("metadata_json", deadLetter => deadLetter.Metadata)
    .Map("dead_lettered_at_utc", deadLetter => deadLetter.DeadLetteredAtUtc);
```

## Dapper Execution Model

The PostgreSQL transport uses Dapper internally for execution.

That choice keeps the transport:

- lightweight
- explicit
- parameterized by default
- compatible with consumer-owned SQL

The public API does not expose Dapper types directly. Pipelinez owns the command abstraction and converts it to Dapper internally.

## Connection Options

Shared PostgreSQL options support:

- `ConnectionString`
- `ConfigureConnectionString`
- `ConfigureDataSource`
- `DataSource`
- `CommandTimeoutSeconds`
- `EnableSensitiveLogging`
- `SerializerOptions`

## Related Docs

- [Getting Started: PostgreSQL Destination](../getting-started/postgresql-destination.md)
- [Architecture: PostgreSQL](../architecture/postgresql.md)
- [Dead-Lettering](../guides/dead-lettering.md)
