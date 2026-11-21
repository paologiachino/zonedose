# 0002: Database and role model

Status: accepted (phase 0, 2026-11-21)

## Context

Today the app role reads and writes data only and does not own tables, so RLS applies to it; the migrator role owns the schema and runs migrations. Orchard creates and migrates its tables with the connection it is given. Personal data needs `user_id` and RLS.

## Decision

Keep the split, for Orchard's tables too.

- One PostgreSQL database per environment in `sportlab-postgres` (container name unchanged for now), owned by the migrator role. PostGIS enabled by the superuser at init.
- Tenants use **table prefixes in `public`**, not schemas. YesSql runs `CREATE SCHEMA IF NOT EXISTS` at every tenant start, which the app role cannot do.
- **Migrate step**: setup and every Orchard or module migration run with the migrator connection, as a one-off job before the new version serves traffic (like today's Alembic job). The application then runs with the app role, which needs no DDL at startup.
- The deploy must fail if a migration is still pending: Orchard under the app role logs a failed migration but keeps serving with the old schema.
- Personal data (activities, tracks, Garmin keys) lives in module-owned relational tables, created by module migrations with `user_id`, RLS enabled and a policy on `current_setting('zonedose.user_id', true)`. Every query sets it with `set_config(..., true)` inside the transaction. Content items (YesSql documents) are only for non-personal content.
- Modules access PostGIS through their own `NpgsqlDataSource` with NetTopologySuite, built from the tenant connection string.

## Consequences

- Same security model as today, verified in the spike: as the app role a user sees only their rows, and none without a user set; the app role cannot alter or create tables.
- The migrate step is extra deploy work in phase 6 (a job running the same image with the migrator connection, plus the pending-migration check).
- Backups must cover the Orchard tables as well (phase 2).

Evidence: [spike results](../../spike/README.md), findings 3 and 4.
