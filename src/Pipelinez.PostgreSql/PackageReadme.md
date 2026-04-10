# Pipelinez.PostgreSql

PostgreSQL transport extensions for Pipelinez.

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

## Quick Example

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

## More Information

- Repository: https://github.com/KenBerg75/Pipelinez
- Docs: https://github.com/KenBerg75/Pipelinez/tree/main/docs
