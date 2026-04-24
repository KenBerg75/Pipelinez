# Benchmarking

Audience: maintainers and contributors running repeatable Pipelinez performance benchmarks.

## What This Covers

- the BenchmarkDotNet suite in `src/benchmarks/Pipelinez.Benchmarks`
- the in-memory baseline
- opt-in Docker-backed transport benchmarks
- opt-in Azure Service Bus live benchmarks
- scheduled benchmark artifact publishing

## Benchmark Project Shape

The benchmark project keeps a single BenchmarkDotNet assembly and conditionally registers benchmark types based on environment:

- in-memory benchmarks are always available
- Docker-backed Kafka, RabbitMQ, Amazon S3, PostgreSQL, and SQL Server benchmarks are registered when `PIPELINEZ_BENCH_ENABLE_DOCKER=true`
- Azure Service Bus benchmarks are registered when `PIPELINEZ_ASB_CONNECTION_STRING` is present

This keeps the default command safe on a machine without Docker or cloud credentials.

## Run The Default Suite

From the repository root:

```bash
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks
```

That command runs the always-available in-memory suite.

## Enable Transport Benchmarks

Enable Docker-backed transports:

```bash
PIPELINEZ_BENCH_ENABLE_DOCKER=true dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*Kafka*"
PIPELINEZ_BENCH_ENABLE_DOCKER=true dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*RabbitMq*"
PIPELINEZ_BENCH_ENABLE_DOCKER=true dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*AmazonS3*"
PIPELINEZ_BENCH_ENABLE_DOCKER=true dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*PostgreSql*"
PIPELINEZ_BENCH_ENABLE_DOCKER=true dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*SqlServer*"
```

Enable Azure Service Bus live benchmarks:

```bash
PIPELINEZ_ASB_CONNECTION_STRING="<connection string>" dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*AzureServiceBus*"
```

## Useful Filters

```bash
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*InMemory*"
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*KafkaDestination*"
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*RabbitMqSource*"
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*AmazonS3DeadLetter*"
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*PostgreSqlDestination*"
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*SqlServerDeadLetter*"
```

## Output And Artifacts

BenchmarkDotNet writes reports and generated files under:

- `src/benchmarks/Pipelinez.Benchmarks/BenchmarkDotNet.Artifacts/`
- `src/benchmarks/Pipelinez.Benchmarks/bin/Release/net8.0/`

The benchmark methods also verify end-state correctness before returning. A benchmark run that silently drops work should fail rather than produce misleading timing output.

## Scheduled Publication

The repository includes `.github/workflows/benchmarks.yaml`.

That workflow:

- runs once a month on a schedule
- supports manual dispatch
- runs the in-memory suite on every execution
- runs Docker-backed transport suites with `PIPELINEZ_BENCH_ENABLE_DOCKER=true`
- runs Azure Service Bus suites only when `PIPELINEZ_ASB_CONNECTION_STRING` is configured as a secret
- uploads raw BenchmarkDotNet artifacts for each executed suite
- generates `documentation/guides/benchmark-results.md` from those artifacts
- opens a pull request so benchmark result publication is reviewed before it reaches the docs site when `PIPELINEZ_BENCHMARK_PR_TOKEN` is configured

Configure `PIPELINEZ_BENCHMARK_PR_TOKEN` as a repository secret for the workflow with a PAT or GitHub App token that can push branches and open pull requests. Without that secret, the workflow still generates the results page and artifacts, then reports that pull request creation was skipped.

## Reporting Guidance

For open source publication, treat benchmark numbers as comparative evidence rather than hard SLAs.

When publishing benchmark summaries, include:

- run date
- commit SHA or release tag
- machine or runner context
- .NET version
- enabled transports
- caveats that cross-machine benchmark numbers are inherently noisy
