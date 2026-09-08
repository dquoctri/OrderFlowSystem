# Architecture notes

Terse reference for the rules the code must not break. The narrative walkthrough is in
[solution-overview.md](solution-overview.md); the requirement is
[Coding challenge - Order flow.md](Coding%20challenge%20-%20Order%20flow.md).

## Dependency direction

```text
Web ──HTTP──> BFF ──HTTP──> Orders API / Inventory API   (Payments has no UI caller)

BFF           ──> Orders API, Inventory API    only   (no DB, no broker, no domain contract)

Orders API    ──> PostgreSQL orderflow_orders    only
Inventory API ──> PostgreSQL orderflow_inventory only
Payments API  ──> PostgreSQL orderflow_payments  only

All services ──> OrderFlow.Contracts   (wire DTOs)   — never the reverse
All services ──> OrderFlow.Messaging   (Pulsar plumbing)
All services <──> Apache Pulsar
```

The Web SPA has **one** HTTP origin: the BFF. It no longer knows the Orders/Inventory addresses.

## BFF / API Composition

`src/Web/OrderFlow.Bff` is a stateless ASP.NET Core app that composes the domain services for the
web UI. Rules it must not break:

- **No durable state.** No database, broker, or cache that must be shared between replicas.
  In-process state consists of HTTP connection pools and active SSE fan-out sessions.
- **No domain coupling.** It references no project in `src/` — not `OrderFlow.Contracts`, not an
  entity, not a `DbContext`. It owns its own view-model records under `Bff/Contracts/` and maps
  the downstream JSON onto them, so an internal contract change never reaches the browser.
- **Parallel fan-out under one deadline.** `DashboardComposer` starts every independent downstream
  call before awaiting any (`Task.WhenAll`-style), inside one linked `CancellationTokenSource`
  with a request budget (`Downstream:RequestTimeoutSeconds`, default 3 s).
- **Per-field failure policy.** Orders is *required* — its failure is a `503`. Inventory and the
  event trace are *degradable* — on failure the response omits them and adds a `warnings[]` entry.
- **Replica-safe downstream calls.** Each typed `HttpClient` sets `PooledConnectionLifetime` so
  the connection pool is recycled and Docker DNS re-resolved; traffic keeps spreading when a
  downstream is scaled. `AddStandardResilienceHandler` adds retry + per-attempt timeout +
  circuit breaker (the breaker is per-client, not per-replica).

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

The **BFF** is stateless, so `--scale bff=N` needs no coordination: `edge` round-robins to the
replicas via Docker DNS, and each replica's downstream calls re-resolve DNS (via
`PooledConnectionLifetime`) so they too spread across scaled Orders/Inventory replicas rather than
pinning to the first one resolved.

## Public HTTP ownership (via the `edge` proxy)

| Port | Service | Public responsibilities |
| --- | --- | --- |
| 5000 | Web | Browser UI only. All its API calls go to the BFF (5004). |
| 5001 | Orders | Create an order, query an order + outcome, `GET /orders/{id}/trace`, `GET /orders/{id}/stream`, list a customer's orders, health, DLQ admin. |
| 5002 | Inventory | Query stock, demo stock adjustment, health, DLQ admin. |
| 5003 | Payments | Query a payment, health, DLQ admin. |
| 5004 | BFF | `GET /dashboard` and `GET /dashboard/orders/{id}` (composed views); `GET /dashboard/orders/{id}/stream` (SSE fan-out); `POST /orders`, `GET /orders/{id}`, `GET /orders/{id}/trace` (pass-through to Orders); health. No business logic, no persistence. |

Ports 5001–5003 stay published: the demo script and integration tests hit the services directly,
and it is useful to show the SPA using only 5004 while the services remain independently reachable.

## Live updates (SSE)

Orders produces `GET /orders/{id}/stream` from `order_saga_log`. The insert trigger notifies
`order_saga_log` after commit; one dedicated PostgreSQL LISTEN connection per Orders replica
signals its subscribers. Each subscriber re-queries after its sequence cursor, including every
15 seconds and after listener reconnect, so coalesced or missed notifications cannot lose rows.
Consumers lock the order row before allocating a sequence: different topics can run concurrently,
so KeyShared alone cannot prevent duplicate SSE cursor IDs.
Replay reads order status and log rows in one repeatable-read snapshot. Payment-declined streams
wait for `StockReleased` before emitting `terminal`; the existing order status transition is unchanged.

The BFF forwards opaque `saga` and `terminal` frames at `GET /dashboard/orders/{id}/stream`.
One upstream connection per active order per BFF replica serves local subscribers, with in-memory
replay for late joiners. Last subscriber departure cancels and disposes the upstream. Slow
subscribers are disconnected to replay, rather than silently losing events. Upstream loss emits
`stalled`, retries with backoff, and resumes with `Last-Event-ID`. The separate named stream HTTP
client has a connect timeout and pooled connection lifetime, without the standard request timeout.

A browser connection stays on one BFF replica for its lifetime. Different BFF replicas can hold
separate upstreams for the same order; no shared cache or backplane is required. Any Orders
replica sees any other replica's committed inserts through PostgreSQL notifications. Budget one
additional database connection per Orders replica (three extra at the documented demo scale;
PostgreSQL's default 100-connection limit leaves headroom, subject to ordinary request load).

The SPA uses one native EventSource per tracked order, feeding both the timeline and event cards.
Only dashboard tables poll, every five seconds. After three connection errors the shared client
falls back to the composed tracked-order endpoint every five seconds. Both SSE endpoints cap
connections at ten minutes, send 15-second heartbeats, and disable nginx buffering. Browser
reconnects use `Last-Event-ID`; malformed cursors return 400 and unknown orders return 404.
