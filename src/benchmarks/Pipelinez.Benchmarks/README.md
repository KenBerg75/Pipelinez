# Pipelinez.Benchmarks

This project contains the BenchmarkDotNet-based performance benchmarks for Pipelinez.

## Prerequisites

- .NET 8 SDK installed
- run benchmarks in `Release` mode for meaningful results
- Docker only when running Kafka, RabbitMQ, Amazon S3, PostgreSQL, or SQL Server transport benchmarks
- an Azure Service Bus connection string only when running Azure Service Bus live benchmarks

## Default Run

From the repository root:

```bash
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks
```

That command runs the always-available in-memory suite.

## Enable Transport Benchmarks

Register Docker-backed transport benchmarks:

```bash
PIPELINEZ_BENCH_ENABLE_DOCKER=true dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*Kafka*"
PIPELINEZ_BENCH_ENABLE_DOCKER=true dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*RabbitMq*"
PIPELINEZ_BENCH_ENABLE_DOCKER=true dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*AmazonS3*"
PIPELINEZ_BENCH_ENABLE_DOCKER=true dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*PostgreSql*"
PIPELINEZ_BENCH_ENABLE_DOCKER=true dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*SqlServer*"
```

Register Azure Service Bus live benchmarks:

```bash
PIPELINEZ_ASB_CONNECTION_STRING="<connection string>" dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*AzureServiceBus*"
```

## Run A Specific Benchmark

Use a BenchmarkDotNet filter:

```bash
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*InMemoryPipelineBenchmarks*"
```

You can also filter down to a specific benchmark method:

```bash
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*TunedParallelPipeline*"
```

## What The Benchmarks Measure

The benchmark suite includes:

- the always-available in-memory baseline
- Kafka source, destination, and dead-letter flows
- RabbitMQ source, destination, and dead-letter flows
- Amazon S3 source, destination, and dead-letter flows
- Azure Service Bus source, destination, and dead-letter flows when live credentials are available
- PostgreSQL destination and dead-letter flows
- SQL Server destination and dead-letter flows

BenchmarkDotNet reports metrics such as:

- mean execution time
- memory allocations
- throughput-oriented comparisons between benchmark cases

## Output

BenchmarkDotNet writes generated artifacts and reports under the benchmark project's output folders.

Look under:

- `src/benchmarks/Pipelinez.Benchmarks/bin/Release/net8.0/`
- `src/benchmarks/Pipelinez.Benchmarks/BenchmarkDotNet.Artifacts/`

The repository also includes a scheduled benchmark workflow at `.github/workflows/benchmarks.yaml` that uploads artifacts for monthly and manually triggered runs.

## Notes

- Avoid running benchmarks while the machine is under heavy load.
- Prefer running the same command multiple times when comparing changes.
- Benchmark results are for relative comparison and regression detection, not absolute guarantees across machines.
- Raw BenchmarkDotNet artifacts should be uploaded or attached to benchmark runs rather than committed into the repository history on every execution.
