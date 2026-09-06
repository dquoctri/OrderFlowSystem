# OrderFlow demo script

Goal of the demo: show a **successful order** and a **payment failure with stock correctly
restored**, and be able to point at *why* each transition happened.

Run everything from the `OrderFlowSystem/` directory. Needs Docker Desktop / Engine with
Compose v2 (`docker compose`, not the legacy `docker-compose` binary).

---

## 0. Start the stack

```bash
cp .env.example .env          # first run only
docker compose up --build
```

Wait until `orders-api`, `inventory-api`, `payments-api` report healthy. In another terminal:

```bash
curl -s http://localhost:5001/health/ready
curl -s http://localhost:5002/health/ready
curl -s http://localhost:5003/health/ready
```

Open the UI at <http://localhost:5000>. Keep **/dashboard** open in one tab — it auto-refreshes
every 500 ms and is the single screen that tells the whole story.

Seed stock: `WIDGET-01` = 10 on hand, `WIDGET-02` = 5 on hand.

---

## 1. Happy path — order confirmed, stock permanently consumed

Total must **not** end in `.99`.

### Via the UI
1. **Place order** page → customer `demo-success`, line `WIDGET-01 × 1 @ 10.00` → *Place order*.
2. Watch the status trail: `Pending → Reserving → Charging → Confirmed`.
3. On **/dashboard**: `WIDGET-01` goes `available 10 → 9` (reserved briefly `1`, then on-hand drops to `9`).

### Via curl
```bash
curl -i -X POST http://localhost:5001/orders \
  -H 'Content-Type: application/json' \
  -d '{"customerId":"demo-success","lines":[{"sku":"WIDGET-01","quantity":1,"unitPrice":10.00}]}'
# copy orderId from the response, then:
ORDER=<orderId>
curl -s http://localhost:5001/orders/$ORDER      # status → Confirmed, reservationCompleted+paymentCompleted true
curl -s http://localhost:5003/payments/$ORDER    # status → Succeeded
curl -s http://localhost:5002/stock              # WIDGET-01 quantityOnHand 9, quantityReserved 0
```

### Show why (three ways)
- **UI:** the place-order page shows a green outcome banner ("Confirmed — customer charged … and
  stock permanently consumed") and a live **event journey** — one card per saga event, badged by
  the service that published it (orders / inventory / payments).
- **API:** `curl -s http://localhost:5001/orders/$ORDER/trace` — the ordered saga log
  (`OrderPlaced → ReservationSucceeded → PaymentSucceeded`) with a `detail` payload per event.
- **Logs:** `docker compose logs --no-color orders-api inventory-api payments-api | grep $ORDER`
  — the same story from the structured logs, across three containers, in order.

**Point to make:** on success Inventory *reduces `quantity_on_hand`* and marks the reservation
`Consumed` — the stock is gone from the pool, not just held. (See solution-overview.md §5.)

---

## 2. Failure path — payment fails, stock restored exactly

The fake gateway rejects any total whose cents are `.99`
([FakePaymentGateway.cs](../src/Payments/OrderFlow.Payments.Api/Infrastructure/Payments/FakePaymentGateway.cs)).

### Before
```bash
curl -s http://localhost:5002/stock    # note WIDGET-02: quantityOnHand 5, quantityReserved 0, available 5
```

### Trigger
UI: **Place order** → customer `demo-failure`, line `WIDGET-02 × 1 @ 10.99` → *Place order*.
Status trail: `Pending → Reserving → Charging → Cancelled`. The page shows a **red banner**:
*"Payment was declined — the fake payment gateway rejects totals ending in .99. Any reserved
stock has been released."* and the event journey ends `Payment declined → Stock released`.

```bash
curl -i -X POST http://localhost:5001/orders \
  -H 'Content-Type: application/json' \
  -d '{"customerId":"demo-failure","lines":[{"sku":"WIDGET-02","quantity":1,"unitPrice":10.99}]}'
ORDER=<orderId>
curl -s http://localhost:5001/orders/$ORDER        # status Cancelled, failureStage "PaymentDeclined", failureReason "..."
curl -s http://localhost:5001/orders/$ORDER/trace  # OrderPlaced · ReservationSucceeded · PaymentFailed · StockReleased
curl -s http://localhost:5003/payments/$ORDER      # status → Failed
```

### After — the important assertion
```bash
curl -s http://localhost:5002/stock    # WIDGET-02 back to quantityOnHand 5, quantityReserved 0, available 5
```
Stock is **exactly** what it was before the order. Nothing lost, nothing double-counted.

### Show why
```bash
docker compose logs --no-color orders-api inventory-api payments-api | grep $ORDER
```
`ReservationSucceeded` (reserved) → `PaymentFailed` → Inventory's `PaymentFailedConsumer`
releases the reservation (`reserved -= qty`, reservation `→ Released`, emits `StockReleased`) →
Orders `→ Cancelled`. The compensation is an event-driven action, not a DB rollback.

---

## 3. Optional — concurrency on the last unit

Bring one SKU down to a single unit and race two orders:

```bash
curl -s -X POST http://localhost:5002/stock/WIDGET-01/adjust -H 'Content-Type: application/json' -d '{"quantity":1}'

curl -s -X POST http://localhost:5001/orders -H 'Content-Type: application/json' \
  -d '{"customerId":"race-a","lines":[{"sku":"WIDGET-01","quantity":1,"unitPrice":10.00}]}' &
curl -s -X POST http://localhost:5001/orders -H 'Content-Type: application/json' \
  -d '{"customerId":"race-b","lines":[{"sku":"WIDGET-01","quantity":1,"unitPrice":10.00}]}' &
wait
```

Exactly one order reaches `Confirmed`; the other ends `Cancelled` with a `ReservationFailed`.
`WIDGET-01` never goes below `available 0`. The serialization point is
`SELECT … FOR UPDATE` in `InventoryEventConsumer.LockStockAsync`.

---

## 4. Optional — poison message / DLQ

```bash
# inspect (non-destructive)
curl -s 'http://localhost:5002/admin/dead-letters?topic=order-placed&limit=20'
# replay everything dead-lettered for a topic back onto the original topic
curl -s -X POST 'http://localhost:5002/admin/dead-letters/replay?topic=order-placed&limit=20'
```

To force a message into the DLQ live: `docker compose stop inventory-db` mid-order, watch the
backed-off retries in the logs (2 s / 5 s / 10 s), then after 3 attempts the message lands in
`orderflow.order-placed.dlq`. `docker compose start inventory-db`, replay, and the order still
reaches a terminal state.

---

## 5. Reset / teardown

```bash
docker compose down                 # stop, keep data volumes
docker compose down -v              # stop and DELETE the three postgres volumes (fresh seed next up)
```

To re-run the demo from clean seed stock without a full teardown, use `down -v` then
`docker compose up --build`.

---

## 6. Automated equivalent

`tests/Integration.Tests/ComposeSagaTests.cs` runs both §1 and §2 against the running stack:

```bash
docker compose up --build            # leave running
dotnet test --filter "Category=Integration"
```
