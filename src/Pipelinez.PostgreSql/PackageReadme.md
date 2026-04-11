# Pipelinez.PostgreSql

PostgreSQL transport extensions for Pipelinez.

Use `Pipelinez.PostgreSql` when a Pipelinez pipeline needs to write successful records or dead-letter records to PostgreSQL tables owned by your application.

## What This Package Does

`Pipelinez.PostgreSql` adds:

- `WithPostgreSqlDestination(...)`
- `WithPostgreSqlDeadLetterDestination(...)`
- consumer-owned table mapping through `PostgreSqlTableMap<T>`
- custom parameterized SQL execution through `PostgreSqlCommandDefinition`
- Dapper-backed PostgreSQL writes for normal and dead-letter flows

## Install

```bash
dotnet add package Pipelinez.PostgreSql
```

`Pipelinez.PostgreSql` depends on `Pipelinez`, so you do not need to add both explicitly unless you prefer to do so.

## When To Use This Package

Use this package when PostgreSQL is a pipeline destination or dead-letter store. The package does not require a Pipelinez-owned schema; your application controls table names, column names, and custom SQL.

## Minimal Example

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
            .MapJson("payload", record => record))
    .Build();

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }
}
```

## Common Recipes

- Write successful pipeline records to a PostgreSQL table.
- Write dead-letter records to a PostgreSQL dead-letter table.
- Use `PostgreSqlTableMap<T>` for generated inserts into consumer-owned tables.
- Use `PostgreSqlCommandDefinition` for custom parameterized SQL.
- Combine `Pipelinez.Kafka` with `Pipelinez.PostgreSql` to persist processed Kafka records.

## Related Packages

- [`Pipelinez`](https://www.nuget.org/packages/Pipelinez)
  core pipeline runtime.
- [`Pipelinez.Kafka`](https://www.nuget.org/packages/Pipelinez.Kafka)
  Kafka source, destination, dead-lettering, distributed execution, and partition-aware scaling.

## Documentation

- NuGet: https://www.nuget.org/packages/Pipelinez.PostgreSql
- Repository: https://github.com/KenBerg75/Pipelinez
- API reference: https://kenberg75.github.io/Pipelinez/api/
- Getting started: https://github.com/KenBerg75/Pipelinez/blob/main/docs/getting-started/postgresql-destination.md
- PostgreSQL docs: https://github.com/KenBerg75/Pipelinez/blob/main/docs/transports/postgresql.md
