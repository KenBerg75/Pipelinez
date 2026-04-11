# Documentation

This folder contains the main documentation set for Pipelinez.

## Start Here

- [Overview](Overview.md)
  architectural overview of the current runtime
- [Generated API Reference](https://kenberg75.github.io/Pipelinez/api/Pipelinez.Core.html)
  browsable API documentation generated from public XML docs
- [API Stability](ApiStability.md)
  public API compatibility policy and maintainer workflow

## Getting Started

- [In-Memory Pipeline](getting-started/in-memory.md)
  first end-to-end pipeline without external infrastructure
- [Kafka Pipeline](getting-started/kafka.md)
  first Kafka-backed pipeline using the example Docker workflow
- [PostgreSQL Destination](getting-started/postgresql-destination.md)
  first PostgreSQL-backed destination and dead-letter pipeline shape

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
- [PostgreSQL](transports/postgresql.md)

## Operations

- [Troubleshooting](operations/troubleshooting.md)
- [Repository Topics](operations/repository-topics.md)
- [Security Automation](operations/security-automation.md)

## Architecture

- [Runtime](architecture/runtime.md)
- [Kafka Internals](architecture/kafka.md)
- [PostgreSQL Internals](architecture/postgresql.md)
- [Testing](architecture/testing.md)

## Installation Note

The public packages are available on NuGet.org:

- [`Pipelinez`](https://www.nuget.org/packages/Pipelinez)
- [`Pipelinez.Kafka`](https://www.nuget.org/packages/Pipelinez.Kafka)
- [`Pipelinez.PostgreSql`](https://www.nuget.org/packages/Pipelinez.PostgreSql)

Install with:

```bash
dotnet add package Pipelinez
dotnet add package Pipelinez.Kafka
dotnet add package Pipelinez.PostgreSql
```

Public releases are configured through tag-based GitHub Actions and NuGet Trusted Publishing.

All public packages also ship XML IntelliSense documentation so API descriptions show up directly in supported IDEs. The same XML documentation is used to generate the public API reference at https://kenberg75.github.io/Pipelinez/api/Pipelinez.Core.html.
