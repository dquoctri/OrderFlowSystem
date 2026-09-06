# postgres/

Intentional placeholder. The per-service database schema is created by the
`orders-migrator`, `inventory-migrator`, and `payments-migrator` one-shot services in
`docker-compose.yml` (EF Core migrations under each API's `Infrastructure/Persistence/Migrations/`).
Seed stock (`WIDGET-01`, `WIDGET-02`) is in the Inventory initial migration.

Put SQL bootstrap assets here only if you move away from migrator containers.
