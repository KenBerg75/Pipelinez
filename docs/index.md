# Pipelinez Documentation

Pipelinez is a .NET 8 library for building typed, observable source -> segment -> destination record-processing pipelines with retries, dead-lettering, flow control, performance snapshots, health/metrics, and transport extensions.

## Start Here

- <a href="https://kenberg75.github.io/Pipelinez/articles/README.html">Conceptual documentation</a>
- <a href="https://kenberg75.github.io/Pipelinez/api/index.html">API reference</a>
- <a href="https://kenberg75.github.io/Pipelinez/articles/getting-started/in-memory.html">In-memory quickstart</a>
- <a href="https://kenberg75.github.io/Pipelinez/articles/getting-started/kafka.html">Kafka quickstart</a>
- <a href="https://kenberg75.github.io/Pipelinez/articles/getting-started/postgresql-destination.html">PostgreSQL destination quickstart</a>

## Packages

| Package | Purpose |
| --- | --- |
| `Pipelinez` | Core typed pipeline runtime |
| `Pipelinez.Kafka` | Kafka source, destination, dead-lettering, distributed execution, and partition-aware scaling |
| `Pipelinez.PostgreSql` | PostgreSQL destination and dead-letter writes with consumer-owned schema mapping |

## API Reference

The API reference is generated from the public XML documentation comments in the package projects. Missing public XML documentation is treated as a build error for package projects, so the generated reference should remain aligned with the package IntelliSense experience.
