# Documentation

This folder contains the main documentation set for Pipelinez.

## Start Here

- [Overview](Overview.md)
  architectural overview of the current runtime
- [API Stability](ApiStability.md)
  public API compatibility policy and maintainer workflow

## Getting Started

- [In-Memory Pipeline](getting-started/in-memory.md)
  first end-to-end pipeline without external infrastructure
- [Kafka Pipeline](getting-started/kafka.md)
  first Kafka-backed pipeline using the example Docker workflow

## Guides

- [Lifecycle](guides/lifecycle.md)
- [Error Handling](guides/error-handling.md)
- [Retry](guides/retry.md)
- [Dead-Lettering](guides/dead-lettering.md)
- [Flow Control](guides/flow-control.md)
- [Distributed Execution](guides/distributed-execution.md)
- [Performance](guides/performance.md)
- [Operational Tooling](guides/operational-tooling.md)

## Transport Docs

- [Kafka](transports/kafka.md)

## Operations

- [Troubleshooting](operations/troubleshooting.md)

## Architecture

- [Runtime](architecture/runtime.md)
- [Kafka Internals](architecture/kafka.md)
- [Testing](architecture/testing.md)

## Installation Note

Packaging is now configured for:

- `Pipelinez`
- `Pipelinez.Kafka`

Expected install commands for published packages are:

```bash
dotnet add package Pipelinez
dotnet add package Pipelinez.Kafka
```

Release/version automation is tracked separately, so this docs set still focuses on the current repository and example workflow as the most directly runnable path.
