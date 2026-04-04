# Testing Architecture

Audience: contributors and maintainers extending the test suite.

## What This Covers

- the current test layers
- what belongs in core tests versus Kafka integration tests
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

## API Approval Tests

The repository now also includes public API approval tests for:

- `Pipelinez`
- `Pipelinez.Kafka`

Those tests compare the compiled public surface to checked-in approved baselines.

Baseline refresh workflow:

```powershell
$env:PIPELINEZ_UPDATE_API_BASELINES='1'
dotnet test src/tests/Pipelinez.Tests/Pipelinez.Tests.csproj --filter ApiApprovalTests
dotnet test src/tests/Pipelinez.Kafka.Tests/Pipelinez.Kafka.Tests.csproj --filter ApiApprovalTests
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

## Related Docs

- [Overview](../Overview.md)
- [API Stability](../ApiStability.md)
- [Contributing](../../CONTRIBUTING.md)
