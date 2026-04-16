# RabbitMQ Architecture

Audience: maintainers and contributors working on the RabbitMQ transport internals.

## Package Boundary

RabbitMQ support lives in `Pipelinez.RabbitMQ`.

The package owns:

- connection and channel creation
- optional topology declaration and passive validation
- queue source consumption
- exchange/routing-key destination publishing
- Pipelinez dead-letter publishing
- RabbitMQ metadata and header mapping
- competing-consumer distributed execution reporting

The core `Pipelinez` package remains transport-agnostic.

## Source Settlement

`RabbitMqPipelineSource<T>` uses manual RabbitMQ acknowledgements.

Each delivery is registered as pending before it is published into the Pipelinez runtime. The source waits for terminal Pipelinez handling before settling the delivery:

- completion maps to ack
- skip maps to ack
- Pipelinez dead-letter maps to ack by default
- stop and rethrow map to nack with requeue by default
- configured native RabbitMQ dead-letter behavior maps Pipelinez dead-letter to nack without requeue

The source never uses multi-ack in the first implementation. Single-delivery settlement keeps concurrent delivery handling simple and avoids acknowledging unrelated messages.

## Destination Reliability

`RabbitMqPipelineDestination<T>` publishes through a long-lived channel protected by a semaphore.

Publisher confirms and mandatory publishing default to enabled:

- publisher confirms ensure RabbitMQ accepted or rejected the publish
- mandatory returns detect unroutable messages
- destination execution completes only after the publish has succeeded

Channel pooling and batched publishing are intentionally deferred until benchmarks show a need.

## Distributed Execution

RabbitMQ has competing consumers, not partition ownership.

The source reports a logical queue lease so Pipelinez distributed status can show which worker is consuming which queue. This lease is observational; RabbitMQ still owns actual message distribution.

## Topology

Topology declaration is opt-in through `RabbitMqTopologyOptions`.

Production deployments should usually manage exchanges, queues, bindings, and DLX policies outside the application. Passive validation is available when applications should fail fast if required topology is absent.

## Testing

Unit tests use fake RabbitMQ channels to exercise configuration, mapping, settlement, and destination behavior without a broker.

Integration tests use Testcontainers-backed RabbitMQ when Docker is available. In environments without Docker, the integration checks return early while unit and API approval coverage still run.
