# SQL Server Internals

Audience: contributors and maintainers working on the SQL Server transport.

## What This Covers

- destination responsibilities
- dead-letter responsibilities
- mapping and SQL generation
- connection ownership rules

## Transport Scope

The current SQL Server transport is destination-only.

That means the transport does not participate in:

- record sourcing
- source ownership
- replay detection
- polling loops
- CDC or change tracking reads

Instead, it integrates with the destination and dead-letter extension points already provided by the core runtime.

## Destination Responsibilities

`SqlServerPipelineDestination<TRecord>` is responsible for:

- receiving successfully processed records from the pipeline destination base
- translating each record into either:
  - a generated insert command from `SqlServerTableMap<T>`
  - a consumer-provided `SqlServerCommandDefinition`
- executing the final command through Dapper
- only completing the record after SQL Server acknowledges the write

## Dead-Letter Responsibilities

`SqlServerDeadLetterDestination<TRecord>` is responsible for:

- receiving `PipelineDeadLetterRecord<TRecord>` envelopes
- translating each dead-letter record into either:
  - a generated insert command from `SqlServerTableMap<PipelineDeadLetterRecord<T>>`
  - a consumer-provided `SqlServerCommandDefinition`
- executing the final command through Dapper

## Mapping Model

`SqlServerTableMap<T>` is the generated-command path.

It captures:

- schema name
- table name
- ordered column mappings
- whether each mapping is scalar or JSON

The internal command factory then turns that into a parameterized `INSERT`.

For JSON mappings, the generated SQL passes JSON as text parameters. The target table can enforce JSON validity with `ISJSON(...)` constraints.

## SQL Safety

The transport never interpolates record values into SQL text.

Safety rules:

- table-map values become parameters
- custom SQL values are expected to be parameterized through `SqlServerCommandDefinition.Parameters`
- schema, table, and column identifiers are validated and bracket-quoted through the internal identifier helper
- closing brackets in identifiers are escaped
- line breaks, null characters, and semicolons are rejected in mapped identifiers

## Connection Ownership

The connection factory opens `SqlConnection` instances from the configured connection string.

Rules:

- the consumer provides the connection string
- optional `ConfigureConnectionString` mutates a `SqlConnectionStringBuilder` before use
- each command opens and disposes its own connection, letting the SQL Server provider manage pooling
- Pipelinez does not own schema migration or database creation

## Runtime Integration

The SQL Server destination inherits the core destination runtime behavior, so SQL Server writes automatically participate in:

- retry handling
- flow control
- destination performance metrics
- operational health and diagnostics
- dead-letter policy routing

## Related Docs

- [SQL Server](../transports/sql-server.md)
- [Architecture: Runtime](runtime.md)
