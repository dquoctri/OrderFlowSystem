# Architecture notes

Terse reference for the rules the code must not break. The narrative walkthrough is in
[solution-overview.md](solution-overview.md); the requirement is
[Coding challenge - Order flow.md](Coding%20challenge%20-%20Order%20flow.md).

## Dependency direction

```text
Web ──HTTP──> Orders API / Inventory API        (Payments has no UI caller)

Orders API    ──> PostgreSQL orderflow_orders    only
Inventory API ──> PostgreSQL orderflow_inventory only
Payments API  ──> PostgreSQL orderflow_payments  only

All services ──> OrderFlow.Contracts   (wire DTOs)   — never the reverse
All services ──> OrderFlow.Messaging   (Pulsar plumbing)
All services <──> Apache Pulsar
```

`OrderFlow.Contracts` is restricted to message envelopes, payload DTOs, and
serialization-safe primitives. No entities, `DbContext`, repositories, or business services.
`OrderFlow.Messaging` holds the Pulsar client wrapper, the consumer base worker, the redelivery
policy, and topic/subscription constants — no service persistence or business rules.

## State invariants

- One GUID is `orderId` **and** `correlationId` for the whole saga.
- An `outbox_messages` row is inserted in the same transaction as every event-producing state change.
- A consumer inserts the `inbox_messages` row for an `eventId` in the same transaction as the state
  change it causes; a duplicate `eventId` is a no-op.
- A reservation goes only `Active → Released` or `Active → Consumed`. Re-releasing or re-consuming
  is a no-op.
- `Consumed`: `quantity_on_hand -= qty`, `quantity_reserved -= qty`, reservation `Consumed` — one tx.
- `Released`: `quantity_reserved -= qty`, reservation `Released` — one tx.
- `available = quantity_on_hand - quantity_reserved` is never negative; checked before every decrement.
- Order status transitions are guarded: a terminal status (`Confirmed` / `Cancelled`) is never
  overwritten by a later event. The rules live in one place —
  `OrderFlow.Orders.Infrastructure.OrderSagaTransitions`.
- On cancellation the order records `FailureStage` (`ReservationRejected` / `PaymentDeclined`) and
  the failing event's `FailureReason`, in the same transaction as the status change.
- Every Orders consumer appends one `order_saga_log` row per event (unique per
  `(orderId, eventType)`) in its transaction; `GET /orders/{id}/trace` reads it back.

## Messaging

- Wire shape: `WireEvent { EventType, EventId, CorrelationId, OrderId, Timestamp, Data, SchemaVersion=1 }`.
  A consumer rejects an unknown `SchemaVersion`.
- Partitioned topics (4 partitions), one `.dlq` topic each. Created by the `pulsar-init` Compose
  step; the list must stay in sync with `PulsarTopics.cs`.
- Subscriptions are `KeyShared`, message key = `orderId` — per-order ordering, cross-order parallelism.
  One stable subscription name per stream, shared by all replicas of a service:
  `orderflow.orders.saga.v1`, `orderflow.orders.status.v1`, `orderflow.inventory.saga.v1`,
  `orderflow.payments.saga.v1`.
- Failure: log → redeliver after 2 s / 5 s / 10 s (detached) → after 3 attempts publish to
  `<topic>.dlq` and ack. DLQ is inspectable and replayable via `/admin/dead-letters`.

## Multi-instance processing

The HTTP APIs hold no process-local saga state; every replica reads and writes only its own
database. Outbox relay workers claim rows with `FOR UPDATE SKIP LOCKED` + a lease timeout, so a
crashed publisher's row is recovered by another replica; duplicate delivery is absorbed by the
inbox. Schema migrations run once in the `*-migrator` one-shot containers, never per replica.

## Public HTTP ownership (via the `edge` proxy)

| Port | Service | Public responsibilities |
| --- | --- | --- |
| 5000 | Web | Browser UI and polling only. |
| 5001 | Orders | Create an order, query an order + outcome, `GET /orders/{id}/trace`, list a customer's orders, health, DLQ admin. |
| 5002 | Inventory | Query stock, demo stock adjustment, health, DLQ admin. |
| 5003 | Payments | Query a payment, health, DLQ admin. |
