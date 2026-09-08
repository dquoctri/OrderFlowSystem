# OrderFlowSystem
A small order-fulfilment system built for the **Order Flow coding challenge**.
Three .NET microservices coordinate a customer order through reservation, payment, and either
confirmation or compensation — using a **choreographed saga over Apache Pulsar**, one PostgreSQL
database per service, a **BFF** that composes the services for the UI, and a Blazor WebAssembly client.

The point of the exercise is distributed state across service boundaries: the system must show a
**successful order** and a **payment failure with stock restored exactly**, with no stock lost or
double-counted at any point.

## What it does

```
Customer places order ─▶ Orders (Pending)
                          │  OrderPlaced
                          ▼
                        Inventory reserves stock  ──▶ ReservationSucceeded / ReservationFailed
                          │                                    │
      ReservationSucceeded│                                    ▼ (failed) Orders → Cancelled
                          ▼
                        Payments charges (fake gateway)
                          │
             ┌────────────┴─────────────┐
   PaymentSucceeded                PaymentFailed
             ▼                          ▼
 Orders → Confirmed           Orders → Cancelled
 Inventory consumes stock     Inventory releases the reservation (compensation)
 (quantity_on_hand -= qty)    (stock returns to its exact prior level)
```

The fake payment gateway **fails any order total whose cents are `.99`** (e.g. `10.99`), so the
failure path is deterministic to demo.

Full walkthrough with file references: **[docs/solution-overview.md](docs/solution-overview.md)**.

## Stack

| | |
| --- | --- |
| Backend | C# / .NET 10, ASP.NET Core minimal APIs, EF Core 10, `BackgroundService` workers |
| Client | Blazor WebAssembly (static SPA behind nginx) → single origin: the BFF |
| BFF | ASP.NET Core, stateless — **API Composition** for the dashboard (parallel fan-out, per-field degradation) |
| Database | PostgreSQL 16 — **one isolated database + schema per service** |
| Messaging | Apache Pulsar 3.2 standalone — `KeyShared` subscriptions keyed by `orderId`, DLQ per topic |
| Patterns | Transactional outbox, inbox dedup, `SELECT … FOR UPDATE` stock locking, choreographed saga (no orchestrator, no saga framework), BFF + API Composition |
| Runtime | Docker Compose — one-shot migrator containers, single `edge` proxy owning host ports 5000–5004 |

## Project map

```text
OrderFlowSystem/
├── OrderFlow.sln
├── Directory.Build.props            # shared: net10.0, nullable, warnings-as-errors
├── Directory.Packages.props         # central NuGet versions (CPM)
├── global.json                      # .NET SDK pin for local builds (rollForward: latestFeature)
├── docker-compose.yml               # full local stack (pulsar, 3× db, 3× api, 3× migrator, web, edge)
├── docker-compose.prod.yml          # overlay: mandatory secrets + real resource limits
├── .env.example                     # dev DB passwords — copy to .env
│
├── docs/
│   ├── Coding challenge - Order flow.md   # the requirement (source of truth)
│   ├── solution-overview.md              # ★ what is implemented — diagrams, saga flow, SOLID mapping
│   ├── architecture.md                   # invariants & dependency rules (terse reference)
│   └── demo-script.md                    # ★ how to demo both required flows
│
├── infra/
│   ├── README.md
│   ├── gateway/nginx.conf           # edge proxy: host 5000-5003 → internal services
│   ├── web/nginx.conf               # static-file server for the Blazor SPA
│   ├── postgres/                    # placeholder — schema comes from migrator containers
│   └── pulsar/                      # placeholder — topics come from the pulsar-init step
│
├── src/
│   ├── BuildingBlocks/
│   │   ├── OrderFlow.Contracts/     # wire DTOs + EventEnvelope only (no entities, no DbContext)
│   │   └── OrderFlow.Messaging/     # Pulsar client wrapper, consumer base worker, topics, redelivery policy
│   ├── Orders/OrderFlow.Orders.Api/
│   │   ├── Api/                     # endpoint mapping: orders, /orders/{id}/trace, DLQ admin
│   │   └── Infrastructure/          # OrderSagaTransitions (status rules) · Persistence (+Migrations,
│   │                               #   Entities, order_saga_log) · Messaging (6 consumers + outbox)
│   │                               #   · Health · Options · Database
│   ├── Inventory/OrderFlow.Inventory.Api/   # same shape — stock reserve / consume / release
│   ├── Payments/OrderFlow.Payments.Api/     # same shape — fake gateway + payment events
│   ├── Web/OrderFlow.Bff/           # Backend for Frontend — API Composer for the dashboard;
│   │                               #   stateless, no DB, no project ref into src/
│   │   ├── Api/                     # /dashboard (composed) · /orders/* (pass-through)
│   │   ├── Clients/                 # typed Orders / Inventory HTTP clients
│   │   ├── Composition/             # DashboardComposer — parallel fan-out, per-field degradation
│   │   └── Contracts/               # BFF-owned view models
│   └── Web/OrderFlow.Web/
│       ├── Api/                     # typed OrderFlowApiClient (single origin = BFF) + view models
│       ├── Components/              # SagaTimeline.razor (stepper) · SagaJourney.razor (event feed)
│       └── Pages/                   # PlaceOrder.razor, Dashboard.razor
│
└── tests/
    ├── Directory.Build.props       # inherits root props; relaxes CA1707 for test names
    ├── Orders.Tests/               # OrderSagaTransitions rules + terminal-status guards
    ├── Inventory.Tests/            # unit + Testcontainers: last-unit race / dedup / compensation
    ├── Payments.Tests/             # unit — fake gateway
    └── Integration.Tests/         # ComposeSagaTests — both required flows + /trace over real HTTP + Pulsar
```

**`OrderFlow.Contracts` boundary:** transport-level event DTOs and metadata only. It must never
contain entities, repositories, `DbContext` types, business services, or persistence models.

## Prerequisites

- **.NET 10 SDK** (`dotnet --version` in the `10.0.x` band) — for building and running tests.
- **Docker Desktop / Engine with Compose v2** (`docker compose …`, not the legacy `docker-compose`
  v1 binary — resource limits and `condition:` semantics depend on v2).

> A vendored `./.dotnet/` folder (~700 MB, git-ignored) may be present from an offline build
> machine — it's a **.NET 7** SDK and unusable here. Delete it: `rm -rf .dotnet`.
>
> `global.json` pins SDK `10.0.100` with `rollForward: latestFeature`, so any installed `10.x`
> SDK works. (It previously used `latestPatch`, which fails when only a later feature band such as
> `10.0.400` is installed.)

## Build

```bash
dotnet build OrderFlow.sln            # compiles all projects and tests; 0 warnings expected
```

## Start

```bash
cp .env.example .env                  # first run only — dev-only DB passwords
docker compose up --build
```

Startup order is enforced by Compose: `pulsar` → `pulsar-init` (creates topics) →
`*-db` → `*-migrator` (applies EF Core migrations once) → `*-api` (healthy) → `bff` → `web` + `edge`.

Then open:

| URL | What |
| --- | --- |
| <http://localhost:5000> | Blazor client — **Place order** (outcome banner + live event journey) and **Dashboard** |
| `GET localhost:5004/dashboard` | BFF — stock + recent orders composed from two services in one call |
| `GET localhost:5004/dashboard/orders/{id}` | BFF — one order + its saga trace, composed |
| `GET localhost:5001/orders/{id}` | order status + `failureStage` / `failureReason` / `completedAt` (direct) |
| `GET localhost:5001/orders/{id}/trace` | the ordered saga event log for one order (direct) |
| <http://localhost:5001/health/ready> | Orders — `200` only when its DB + Pulsar are reachable |
| <http://localhost:5002/health/ready> · <http://localhost:5003/health/ready> · <http://localhost:5004/health/ready> | Inventory · Payments · BFF |

Seed stock: `WIDGET-01` = 10 on hand, `WIDGET-02` = 5 on hand.

## Demo

Follow **[docs/demo-script.md](docs/demo-script.md)** — it covers the happy path, the `.99`
failure + compensation, the last-unit concurrency check, and the DLQ inspect/replay, with both
UI steps and `curl` commands, plus the one `docker compose logs | grep <orderId>` line that shows
the saga crossing all three services in order.

## Restart

```bash
# after changing source in one service — rebuild just it and recreate
docker compose up -d --build orders-api

# recreate a container without rebuilding (e.g. picked up an .env change)
docker compose up -d --force-recreate edge

# force a clean image (no layer cache)
docker compose build --no-cache web
docker compose up -d web

# restart a running container in place (no rebuild, keeps data)
docker compose restart inventory-api

# full restart, keep databases
docker compose down && docker compose up --build
```

## Clean up

```bash
docker compose down            # stop + remove containers and network; KEEP database volumes
docker compose down -v         # also delete the 3 postgres volumes → fresh seed stock next start
docker compose down --rmi local --volumes --remove-orphans   # nuke everything this project created
```

Use `down -v` to reset the demo to clean seed stock. Do **not** use it if you want to keep
orders/stock from a previous run.

## Scale (stateless API + BFF replicas)

```bash
docker compose up --build --scale bff=3 --scale orders-api=3 --scale inventory-api=3 --scale payments-api=3
```

Only `edge` publishes host ports; replicas expose only their internal `8080`, and nginx resolves
the scaled names via Docker DNS — so replicas never collide. Migrations still run once (in the
migrator containers), and the outbox relay claims rows with `FOR UPDATE SKIP LOCKED`, so multiple
API replicas are safe. The **BFF is stateless**; its typed downstream clients set
`PooledConnectionLifetime` so each replica re-resolves Docker DNS and its calls keep spreading
across scaled Orders/Inventory replicas instead of pinning to one. If you change replica counts
while running, recreate `edge` so it re-resolves upstreams:
`docker compose up -d --force-recreate edge`.

## Configuration & secrets

- Backend config is **environment variables only** (`Database__ConnectionString`,
  `Pulsar__ServiceUrl`, `Pulsar__AdminUrl`; the BFF uses `Downstream__OrdersBaseUrl` /
  `Downstream__InventoryBaseUrl`), set in `docker-compose.yml`. Names are fixed. Options are
  validated at startup (`ValidateOnStart`) — a bad connection string or URL fails the boot with a
  readable message rather than at first use.
- DB passwords come from `.env` (`${ORDERS_DB_PASSWORD:-…}` with a dev default). `.env` is
  git-ignored. For any non-local deployment, overlay `docker-compose.prod.yml`, which makes every
  `*_DB_PASSWORD` mandatory and pulls it from a real secret store (Docker/Swarm secrets,
  Kubernetes `Secret`, AWS Secrets Manager). **Never commit production credentials.**
- The Blazor client is a static SPA and reads **one** base URL from `wwwroot/appsettings.json`
  (`ApiUrls:Bff`) — its only origin. No per-service addresses, secrets, or connection strings in
  the client.

## Testing

```bash
dotnet test                          # runs everything; Docker-dependent tests SKIP if Docker is absent
dotnet test --filter "Category!=Integration"   # fast tier only

# full tier — needs Docker reachable from this process; ComposeSagaTests also needs the stack up
docker compose up --build            # in one terminal
dotnet test --filter "Category=Integration"
```

| Suite | Needs | Proves |
| --- | --- | --- |
| **Unit** — `Orders.Tests`, `Payments.Tests`, most of `Inventory.Tests`, `Bff.Tests` | nothing | `OrderValidation` (request rules + total), `OrderSagaTransitions` (every status move + guard), `StockOperations` (reserve/release/consume + invariants), `OrderReservation` (all-or-nothing), `FakePaymentGateway` via `IPaymentGateway` (the `.99` rule), `PaymentRecord` (charge → event), `PoisonMessagePolicy` (DLQ after 3), `RedeliveryPolicy` (2/5/10s backoff), `DashboardComposer` (parallel fan-out, Orders-fatal vs Inventory-degradable, budget timeout) |
| `Inventory.Tests/StockReservationConcurrencyTests` (`[SkippableFact]`, Testcontainers) | Docker reachable | `SELECT … FOR UPDATE` serialises the last-unit race; inbox `ON CONFLICT` dedup; `PaymentFailed` restores stock exactly — **skips (not fails)** when Docker is unavailable |
| `Integration.Tests/ComposeSagaTests` | full Compose stack up on `localhost:5001/5002` | happy path confirms + consumes stock; `.99` path cancels + restores stock + reports `failureStage`; `/trace` contents — over real HTTP + Pulsar |
| `Bff.Tests/ComposeBffTests` (`[SkippableFact]`) | full Compose stack up on `localhost:5004` | `GET /dashboard` composes stock + orders; `GET /dashboard/orders/{id}` composes order + trace — **skips** when the stack is down |

> **Testcontainers + WSL:** running `docker compose` *only inside WSL* does **not** expose Docker to
> a Windows `dotnet test` / Visual Studio process, so `StockReservationConcurrencyTests` will skip
> there. Use Docker Desktop (Windows integration on) or set `DOCKER_HOST` to run them from Windows.

## Design in one paragraph

Each service is an autonomous bounded context — its own Postgres container, schema, and
`DbContext`, with **no cross-database access and no synchronous calls during the saga**. Saga
steps travel only as Pulsar events. Every state change that produces an event writes the event
into an `outbox_messages` row in the **same transaction**; a per-service relay publishes it and
an `inbox_messages` table makes every consumer idempotent (Pulsar is at-least-once). Events for
one order share the `orderId` partition key, so they are processed in order. On payment success,
Inventory **reduces `quantity_on_hand`, clears the reservation's `quantity_reserved`, and marks
the reservation `Consumed`** — so `available = on_hand - reserved` stays the one true formula and
the reservation row remains as an audit record. On payment failure, Inventory **releases** the
still-`Active` reservation, returning `quantity_reserved` to its exact prior value — a real
compensating action, not a database rollback. Poison messages are retried with backoff
(2 s/5 s/10 s) and dead-lettered after three attempts, then inspectable and replayable via
`/admin/dead-letters`. The SOLID mapping — one class per saga step, base workers closed for
modification, contracts-only shared assembly, DI-injected validated options — is spelled out
file-by-file in [docs/solution-overview.md §6](docs/solution-overview.md).

The UI at `http://localhost:5000` includes a live SSE saga feed: Orders publishes committed log
rows, the BFF fans them out, and one EventSource updates the tracked order. Dashboard tables
refresh every five seconds. SSE supports `--scale bff=3 --scale orders-api=3` via PostgreSQL
LISTEN/NOTIFY; each Orders replica uses one additional database connection. See the
[live-feed demo](docs/demo-script.md#live-feed-and-reconnect-demo) for replay and reconnect checks.
