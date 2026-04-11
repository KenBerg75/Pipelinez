# Kafka Internals

Audience: contributors and maintainers working on the Kafka transport.

## What This Covers

- source and destination responsibilities
- offset storage model
- distributed ownership and partition-aware execution

## Kafka Source Responsibilities

`KafkaPipelineSource<T, TRecordKey, TRecordValue>` is responsible for:

- consuming records
- mapping Kafka key/value pairs into pipeline records
- copying Kafka headers into record headers
- stamping Kafka and distribution metadata
- surfacing partition ownership into the core runtime
- storing offsets after terminal record handling

## Offset Model

Kafka execution intentionally separates:

- consume-time read
- completion-time offset storage

That means a message is consumed first, processed through the pipeline, and only then treated as safely advanced when the record reaches a terminal handled state.

This is especially important for:

- retry
- dead-lettering
- partition drain behavior
- distributed rebalance handling

## Partition-Aware Execution

Kafka now supports explicit partition-aware scaling controls through `KafkaPartitionScalingOptions`.

Key knobs:

- `ExecutionMode`
- `MaxConcurrentPartitions`
- `MaxInFlightPerPartition`
- `RebalanceMode`
- `EmitPartitionExecutionEvents`

The source also coordinates:

- partition assignment
- partition revocation
- partition draining
- pause/resume for partition-local admission
- highest completed offset tracking

## Destination Responsibilities

`KafkaPipelineDestination<T, TRecordKey, TRecordValue>` is responsible for:

- mapping pipeline records into Kafka messages
- ensuring headers exist
- copying pipeline headers into Kafka headers
- awaiting broker acknowledgement

The record is only considered completed after the broker write succeeds.

## Dead-Letter Destination

`KafkaDeadLetterDestination<T, TRecordKey, TRecordValue>` preserves:

- the original record payload
- dead-letter metadata and fault details
- copied headers

It then writes the mapped envelope to a Kafka dead-letter topic.

## Related Docs

- [Kafka](../transports/kafka.md)
- [Distributed Execution](../guides/distributed-execution.md)
- [Architecture: Runtime](runtime.md)
