# Pipelinez Documentation

Pipelinez is a .NET 8 library for building typed, observable source -> segment -> destination record-processing pipelines with retries, dead-lettering, flow control, performance snapshots, health/metrics, and transport extensions.

## Start Here

- <a href="https://kenberg75.github.io/Pipelinez/articles/README.html">Conceptual documentation</a>
- <a href="https://kenberg75.github.io/Pipelinez/api/Pipelinez.Core.html">API reference</a>
- <a href="https://kenberg75.github.io/Pipelinez/articles/getting-started/in-memory.html">In-memory quickstart</a>
- <a href="https://kenberg75.github.io/Pipelinez/articles/getting-started/kafka.html">Kafka quickstart</a>
- <a href="https://kenberg75.github.io/Pipelinez/articles/getting-started/azure-service-bus.html">Azure Service Bus quickstart</a>
- <a href="https://kenberg75.github.io/Pipelinez/articles/getting-started/rabbitmq.html">RabbitMQ quickstart</a>
- <a href="https://kenberg75.github.io/Pipelinez/articles/transports/amazon-s3.html">Amazon S3 transport</a>
- <a href="https://kenberg75.github.io/Pipelinez/articles/getting-started/postgresql-destination.html">PostgreSQL destination quickstart</a>
- <a href="https://kenberg75.github.io/Pipelinez/articles/getting-started/sql-server-destination.html">SQL Server destination quickstart</a>

## Packages

| Package | Purpose |
| --- | --- |
| `Pipelinez` | Core typed pipeline runtime |
| `Pipelinez.Kafka` | Kafka source, destination, dead-lettering, distributed execution, and partition-aware scaling |
| `Pipelinez.AzureServiceBus` | Azure Service Bus queue/topic sources, destinations, dead-lettering, and competing-consumer workers |
| `Pipelinez.RabbitMQ` | RabbitMQ queue sources, exchange/queue destinations, dead-lettering, and competing-consumer workers |
| `Pipelinez.AmazonS3` | Amazon S3 object sources, object destinations, and dead-letter artifact writes |
| `Pipelinez.PostgreSql` | PostgreSQL destination and dead-letter writes with consumer-owned schema mapping |
| `Pipelinez.SqlServer` | SQL Server destination and dead-letter writes with consumer-owned schema mapping |

## API Reference

The API reference is generated from the public XML documentation comments in the package projects. Missing public XML documentation is treated as a build error for package projects, so the generated reference should remain aligned with the package IntelliSense experience.
