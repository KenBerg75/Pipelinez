# Troubleshooting

Audience: application developers and operators diagnosing common Pipelinez issues.

## Publish Before Start

### Symptom

`PublishAsync(...)` throws before any record is accepted.

### Likely Cause

The pipeline was built but never started.

### Fix

Call `StartPipelineAsync()` before publishing records.

## Start Called Twice

### Symptom

`StartPipelineAsync()` throws on the second call.

### Likely Cause

The runtime is intentionally guarded against double-start.

### Fix

Start the pipeline once and share the running instance.

## Pipeline Never Finishes

### Symptom

`Completion` does not finish when you expect it to.

### Likely Cause

The pipeline is still accepting work or downstream work is still in flight.

### Fix

- call `CompleteAsync()` when ingress is done
- then await `Completion`

## Retry Exhaustion Leads To Fault Handling

### Symptom

A record still faults even though retry is configured.

### Likely Cause

Retries were exhausted and control moved to the error-handler path.

### Fix

Inspect:

- retry policy filters
- `MaxAttempts`
- `OnPipelineRecordRetrying`
- `PipelineErrorContext<T>`

## DeadLetter Returned Without A Destination

### Symptom

The pipeline faults when the error handler returns `DeadLetter`.

### Likely Cause

No dead-letter destination was configured.

### Fix

Configure:

- `WithDeadLetterDestination(...)`
- or `WithKafkaDeadLetterDestination(...)`

## Kafka Disconnect Log With `FAIL`

### Symptom

Kafka test or example output includes a log line containing `FAIL` and a broker disconnect.

### Likely Cause

This is usually a `librdkafka` client log emitted during broker shutdown, teardown, or transient disconnect handling, not a failed xUnit assertion.

### Fix

If tests still pass, treat it as client logging unless accompanied by actual test failures.

## Docker Kafka Example Will Not Start

### Symptom

The Kafka example hangs or times out during startup.

### Likely Cause

Docker is not available locally or the selected image could not be started.

### Fix

- verify Docker is running
- retry the example
- set `PIPELINEZ_EXAMPLE_BOOTSTRAP_SERVERS` to use an existing broker instead

## Distributed Worker Owns No Partitions

### Symptom

`GetRuntimeContext().OwnedPartitions` is empty in distributed mode.

### Likely Cause

Possible causes:

- the source does not currently own partitions
- another worker owns the available partitions
- the source is not a distributed-capable transport

### Fix

- verify the pipeline is using Kafka
- verify `PipelineExecutionMode.Distributed` is enabled
- inspect assignment events such as `OnPartitionsAssigned`

## StartOffsetFromBeginning Did Not Replay Existing Data

### Symptom

A Kafka pipeline does not replay old messages even though `StartOffsetFromBeginning = true`.

### Likely Cause

The consumer group already has committed offsets.

### Fix

Use a new consumer group when you want replay-from-beginning behavior for an existing topic.

## Related Docs

- [Lifecycle](../guides/lifecycle.md)
- [Error Handling](../guides/error-handling.md)
- [Kafka](../transports/kafka.md)
