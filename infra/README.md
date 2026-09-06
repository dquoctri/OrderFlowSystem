# infra/

Runtime infrastructure assets referenced by `docker-compose.yml`.

| Path | Used by | Purpose |
| --- | --- | --- |
| `gateway/nginx.conf` | `edge` service | Single entry proxy. Binds host ports 5000–5003 and forwards to `web`, `orders-api`, `inventory-api`, `payments-api` on their internal `8080`. Resolves scaled service names via Docker DNS (`resolver 127.0.0.11`), so `--scale` needs no host-port changes. |
| `web/nginx.conf` | `web` image (`src/Web/OrderFlow.Web/Dockerfile`) | Serves the Blazor WebAssembly static files with SPA fallback (`try_files … /index.html`). |
| `postgres/` | — | Placeholder. Per-service schema is applied by the `*-migrator` one-shot containers (EF Core migrations), not by SQL bootstrap scripts. Seed stock data lives in the Inventory initial migration. |
| `pulsar/` | — | Placeholder. Topics (partitioned + `.dlq`) are created by the `pulsar-init` Compose step, kept in sync with `src/BuildingBlocks/OrderFlow.Messaging/PulsarTopics.cs`. |

Both nginx configs write their PID to `/tmp/nginx.pid` because the images run as an unprivileged
user that cannot write `/run/nginx.pid`.
