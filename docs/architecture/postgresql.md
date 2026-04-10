# PostgreSQL Internals

Audience: contributors and maintainers working on the PostgreSQL transport.

## What This Covers

- destination responsibilities
- dead-letter responsibilities
- mapping and SQL generation
- connection ownership rules

## Transport Scope

The current PostgreSQL transport is destination-only.

That means the transport does not participate in:

- record sourcing
- source ownership
- replay detection
- polling loops

Instead, it integrates with the destination and dead-letter extension points already provided by the core runtime.

## Destination Responsibilities

`PostgreSqlPipelineDestination<TRecord>` is responsible for:

- receiving successfully processed records from the pipeline destination base
- translating each record into either:
  - a generated insert command from `PostgreSqlTableMap<T>`
  - a consumer-provided `PostgreSqlCommandDefinition`
- executing the final command through Dapper
- only completing the record after PostgreSQL acknowledges the write

## Dead-Letter Responsibilities

`PostgreSqlDeadLetterDestination<TRecord>` is responsible for:

- receiving `PipelineDeadLetterRecord<TRecord>` envelopes
- translating each dead-letter record into either:
  - a generated insert command from `PostgreSqlTableMap<PipelineDeadLetterRecord<T>>`
  - a consumer-provided `PostgreSqlCommandDefinition`
- executing the final command through Dapper

## Mapping Model

`PostgreSqlTableMap<T>` is the generated-command path.

It captures:

- schema name
- table name
- ordered column mappings
- whether each mapping is scalar or JSON

The internal command factory then turns that into a parameterized `INSERT`.

For JSON mappings, the generated SQL uses `::jsonb` casts so structured values can be serialized and inserted safely.

## SQL Safety

The transport never interpolates record values into SQL text.

Safety rules:

- table-map values become parameters
- custom SQL values are expected to be parameterized through `PostgreSqlCommandDefinition.Parameters`
- schema, table, and column identifiers are validated and quoted through the internal identifier helper

## Connection Ownership

The connection factory supports two modes:

- externally supplied `NpgsqlDataSource`
- internally built `NpgsqlDataSource`

Rules:

- externally supplied data sources are consumer-owned
- internally built data sources are created from the configured connection string and builder hooks
- connection-string and data-source customization hooks are only applied when Pipelinez builds the data source itself

## Runtime Integration

The PostgreSQL destination inherits the core destination runtime behavior, so PostgreSQL writes automatically participate in:

- retry handling
- flow control
- destination performance metrics
- operational health and diagnostics
- dead-letter policy routing

## Related Docs

- [PostgreSQL](../transports/postgresql.md)
- [Architecture: Runtime](runtime.md)
