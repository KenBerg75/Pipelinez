# Pipelinez.SqlServer

SQL Server destination and dead-letter extensions for Pipelinez.

Use `Pipelinez.SqlServer` when a Pipelinez pipeline needs to write successful records or dead-letter records to SQL Server tables owned by your application.

## What It Adds

- `WithSqlServerDestination(...)`
- `WithSqlServerDeadLetterDestination(...)`
- consumer-owned table mapping through `SqlServerTableMap<T>`
- custom parameterized SQL execution through `SqlServerCommandDefinition`
- Dapper-backed SQL Server writes for normal and dead-letter flows

## Install

```bash
dotnet add package Pipelinez.SqlServer
```

`Pipelinez.SqlServer` depends on `Pipelinez`, so you do not need to add both explicitly unless you prefer to do so.

## Minimal Destination

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

## Scope

- Write successful pipeline records to a SQL Server table.
- Write dead-letter records to a SQL Server dead-letter table.
- Use `SqlServerTableMap<T>` for generated inserts into consumer-owned tables.
- Use `SqlServerCommandDefinition` for custom parameterized SQL.
- Combine `Pipelinez.Kafka`, `Pipelinez.RabbitMQ`, or `Pipelinez.AzureServiceBus` with `Pipelinez.SqlServer` to persist processed records.

This package does not provide SQL Server source or queue polling support.

## Related Links

- NuGet: https://www.nuget.org/packages/Pipelinez.SqlServer
- Documentation: https://github.com/KenBerg75/Pipelinez/tree/main/documentation
- Getting started: https://github.com/KenBerg75/Pipelinez/blob/main/documentation/getting-started/sql-server-destination.md
- SQL Server docs: https://github.com/KenBerg75/Pipelinez/blob/main/documentation/transports/sql-server.md
