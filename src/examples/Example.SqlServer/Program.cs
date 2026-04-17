using Dapper;
using Microsoft.Data.SqlClient;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;
using Pipelinez.SqlServer;
using Pipelinez.SqlServer.Configuration;
using Pipelinez.SqlServer.Mapping;
using Testcontainers.MsSql;

await using var database = await SqlServerExampleDatabase.StartAsync();

await database.ExecuteAsync("""
    CREATE SCHEMA [app];
    """);

await database.ExecuteAsync("""
    CREATE TABLE [app].[processed_orders] (
        [order_id] nvarchar(128) NOT NULL,
        [payload] nvarchar(max) NOT NULL CHECK (ISJSON([payload]) = 1),
        [processed_at_utc] datetimeoffset NOT NULL
    );
    """);

await database.ExecuteAsync("""
    CREATE TABLE [app].[order_failures] (
        [order_id] nvarchar(128) NOT NULL,
        [fault_component] nvarchar(256) NOT NULL,
        [record_json] nvarchar(max) NOT NULL CHECK (ISJSON([record_json]) = 1),
        [dead_lettered_at_utc] datetimeoffset NOT NULL
    );
    """);

var destinationOptions = new SqlServerDestinationOptions
{
    ConnectionString = database.ConnectionString,
    ConfigureConnectionString = builder => builder.ApplicationName = "Pipelinez.Example.SqlServer"
};

var deadLetterOptions = new SqlServerDeadLetterOptions
{
    ConnectionString = database.ConnectionString,
    ConfigureConnectionString = builder => builder.ApplicationName = "Pipelinez.Example.SqlServer"
};

var pipeline = Pipeline<OrderRecord>.New("sql-server-orders")
    .WithInMemorySource(new object())
    .AddSegment(new ValidateOrderSegment(), new object())
    .WithSqlServerDestination(
        destinationOptions,
        SqlServerTableMap<OrderRecord>.ForTable("app", "processed_orders")
            .Map("order_id", record => record.Id)
            .MapJson("payload", record => record)
            .Map("processed_at_utc", _ => DateTimeOffset.UtcNow))
    .WithSqlServerDeadLetterDestination(
        deadLetterOptions,
        SqlServerTableMap<PipelineDeadLetterRecord<OrderRecord>>.ForTable("app", "order_failures")
            .Map("order_id", deadLetter => deadLetter.Record.Id)
            .Map("fault_component", deadLetter => deadLetter.Fault.ComponentName)
            .MapJson("record_json", deadLetter => deadLetter.Record)
            .Map("dead_lettered_at_utc", deadLetter => deadLetter.DeadLetteredAtUtc))
    .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
    .Build();

await pipeline.StartPipelineAsync();
await pipeline.PublishAsync(new OrderRecord { Id = "A-100", Payload = "ok" });
await pipeline.PublishAsync(new OrderRecord { Id = "A-200", Payload = "fail" });
await pipeline.PublishAsync(new OrderRecord { Id = "A-300", Payload = "ok" });
await pipeline.CompleteAsync();
await pipeline.Completion;

var processed = await database.QueryAsync<string>("""
    SELECT [order_id]
    FROM [app].[processed_orders]
    ORDER BY [order_id];
    """);

var failed = await database.QueryAsync<string>("""
    SELECT [order_id]
    FROM [app].[order_failures]
    ORDER BY [order_id];
    """);

Console.WriteLine("Processed:");
foreach (var orderId in processed)
{
    Console.WriteLine($"  {orderId}");
}

Console.WriteLine("Dead-lettered:");
foreach (var orderId in failed)
{
    Console.WriteLine($"  {orderId}");
}

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }

    public required string Payload { get; init; }
}

public sealed class ValidateOrderSegment : PipelineSegment<OrderRecord>
{
    public override Task<OrderRecord> ExecuteAsync(OrderRecord arg)
    {
        if (arg.Payload == "fail")
        {
            throw new InvalidOperationException($"Order {arg.Id} failed validation.");
        }

        return Task.FromResult(arg);
    }
}

internal sealed class SqlServerExampleDatabase : IAsyncDisposable
{
    private const string DefaultPassword = "P@ssw0rd!2026";
    private readonly MsSqlContainer? _container;

    private SqlServerExampleDatabase(string connectionString, MsSqlContainer? container)
    {
        ConnectionString = connectionString;
        _container = container;
    }

    public string ConnectionString { get; }

    public static async Task<SqlServerExampleDatabase> StartAsync()
    {
        var existingConnectionString = Environment.GetEnvironmentVariable("PIPELINEZ_EXAMPLE_SQLSERVER_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(existingConnectionString))
        {
            return new SqlServerExampleDatabase(existingConnectionString, container: null);
        }

        var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword(DefaultPassword)
            .Build();

        await container.StartAsync().ConfigureAwait(false);
        return new SqlServerExampleDatabase(container.GetConnectionString(), container);
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await connection.ExecuteAsync(sql).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        var rows = await connection.QueryAsync<T>(sql).ConfigureAwait(false);
        return rows.AsList();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }
}
