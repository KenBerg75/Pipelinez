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
- [Azure Service Bus Pipeline](getting-started/azure-service-bus.md)
  first Azure Service Bus queue-backed pipeline shape
- [RabbitMQ Pipeline](getting-started/rabbitmq.md)
  first RabbitMQ queue-backed pipeline shape
- [PostgreSQL Destination](getting-started/postgresql-destination.md)
  first PostgreSQL-backed destination and dead-letter pipeline shape
- [SQL Server Destination](getting-started/sql-server-destination.md)
  first SQL Server-backed destination and dead-letter pipeline shape

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
- [Azure Service Bus](transports/azure-service-bus.md)
- [RabbitMQ](transports/rabbitmq.md)
- [PostgreSQL](transports/postgresql.md)
- [SQL Server](transports/sql-server.md)

## Operations

- [Troubleshooting](operations/troubleshooting.md)
- [Release Checklist](operations/release-checklist.md)
- [Repository Topics](operations/repository-topics.md)
- [Security Automation](operations/security-automation.md)

## Architecture

- [Runtime](architecture/runtime.md)
- [Kafka Internals](architecture/kafka.md)
- [Azure Service Bus Internals](architecture/azure-service-bus.md)
- [RabbitMQ Internals](architecture/rabbitmq.md)
- [PostgreSQL Internals](architecture/postgresql.md)
- [SQL Server Internals](architecture/sql-server.md)
- [Testing](architecture/testing.md)

## Installation Note

The public packages are available on NuGet.org:

- [`Pipelinez`](https://www.nuget.org/packages/Pipelinez)
- [`Pipelinez.Kafka`](https://www.nuget.org/packages/Pipelinez.Kafka)
- [`Pipelinez.AzureServiceBus`](https://www.nuget.org/packages/Pipelinez.AzureServiceBus)
- [`Pipelinez.RabbitMQ`](https://www.nuget.org/packages/Pipelinez.RabbitMQ)
- [`Pipelinez.PostgreSql`](https://www.nuget.org/packages/Pipelinez.PostgreSql)
- [`Pipelinez.SqlServer`](https://www.nuget.org/packages/Pipelinez.SqlServer)

Install with:

```bash
dotnet add package Pipelinez
dotnet add package Pipelinez.Kafka
dotnet add package Pipelinez.AzureServiceBus
dotnet add package Pipelinez.RabbitMQ
dotnet add package Pipelinez.PostgreSql
dotnet add package Pipelinez.SqlServer
```

Public releases are configured through tag-based GitHub Actions and NuGet Trusted Publishing.

All public packages also ship XML IntelliSense documentation so API descriptions show up directly in supported IDEs. The same XML documentation is used to generate the public API reference at https://kenberg75.github.io/Pipelinez/api/Pipelinez.Core.html.
