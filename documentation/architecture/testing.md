# Testing Architecture

Audience: contributors and maintainers extending the test suite.

## What This Covers

- the current test layers
- what belongs in core tests versus transport integration tests
- API approval testing

## Test Layers

### Core Tests

`src/tests/Pipelinez.Tests` covers transport-agnostic runtime behavior such as:

- lifecycle
- segments
- destinations
- faults
- retry
- flow control
- performance
- distributed runtime behavior
- operational tooling

### Kafka Integration Tests

`src/tests/Pipelinez.Kafka.Tests` covers real Kafka behavior using Docker/Testcontainers.

This suite validates:

- source-topic to destination-topic flow
- dead-letter topics
- retry and fault handling with a real broker
- distributed worker ownership and rebalance
- partition-aware execution

### PostgreSQL Integration Tests

`src/tests/Pipelinez.PostgreSql.Tests` covers real PostgreSQL behavior using Docker/Testcontainers.

This suite validates:

- direct destination writes through generated table maps
- custom SQL destination writes
- dead-letter table mapping
- dead-letter custom SQL writes
- connection-configuration validation and API approval coverage

### SQL Server Integration Tests

`src/tests/Pipelinez.SqlServer.Tests` covers real SQL Server behavior using Docker/Testcontainers.

This suite validates:

- direct destination writes through generated table maps
- custom SQL destination writes
- dead-letter table mapping
- dead-letter custom SQL writes
- connection-string customization, identifier quoting, JSON text mapping, and API approval coverage

### Azure Service Bus Tests

`src/tests/Pipelinez.AzureServiceBus.Tests` covers Azure Service Bus transport behavior with focused unit tests and public API approval coverage.

This suite validates:

- connection and entity configuration
- source logical distributed lease reporting
- destination sends through the transport sender abstraction
- dead-letter message publishing metadata
- public API approval coverage

## API Approval Tests

The repository now also includes public API approval tests for:

- `Pipelinez`
- `Pipelinez.Kafka`
- `Pipelinez.AzureServiceBus`
- `Pipelinez.RabbitMQ`
- `Pipelinez.PostgreSql`
- `Pipelinez.SqlServer`

Those tests compare the compiled public surface to checked-in approved baselines.

Baseline refresh workflow:

```powershell
$env:PIPELINEZ_UPDATE_API_BASELINES='1'
dotnet test src/tests/Pipelinez.Tests/Pipelinez.Tests.csproj --filter ApiApprovalTests
dotnet test src/tests/Pipelinez.Kafka.Tests/Pipelinez.Kafka.Tests.csproj --filter ApiApprovalTests
dotnet test src/tests/Pipelinez.AzureServiceBus.Tests/Pipelinez.AzureServiceBus.Tests.csproj --filter ApiApprovalTests
dotnet test src/tests/Pipelinez.RabbitMQ.Tests/Pipelinez.RabbitMQ.Tests.csproj --filter ApiApprovalTests
dotnet test src/tests/Pipelinez.PostgreSql.Tests/Pipelinez.PostgreSql.Tests.csproj --filter ApiApprovalTests
dotnet test src/tests/Pipelinez.SqlServer.Tests/Pipelinez.SqlServer.Tests.csproj --filter ApiApprovalTests
```

Then run:

```bash
dotnet test src/Pipelinez.sln --logger "console;verbosity=minimal"
```

## Test Placement Guidance

Use core tests when:

- the behavior is transport-agnostic
- you can validate it with in-memory or test-double components

Use Kafka integration tests when:

- the behavior depends on a real broker
- the scenario depends on Kafka offsets, partitions, rebalance, or headers

Use Azure Service Bus tests when:

- the behavior depends on Service Bus connection, entity, header, or settlement semantics
- the scenario validates queue/topic destination behavior or competing-consumer metadata

Use PostgreSQL integration tests when:

- the behavior depends on real PostgreSQL execution
- the scenario validates generated SQL, custom SQL, or dead-letter table writes

Use SQL Server integration tests when:

- the behavior depends on real SQL Server execution
- the scenario validates generated SQL, custom SQL, dead-letter table writes, JSON text mapping, or bracket-quoted identifiers

## Related Docs

- [Overview](../Overview.md)
- [API Stability](../ApiStability.md)
- [Contributing](https://github.com/KenBerg75/Pipelinez/blob/main/CONTRIBUTING.md)
