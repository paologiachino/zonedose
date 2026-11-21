#!/bin/sh
# Spike database: one database, two roles, one table prefix per Orchard tenant.
# Mirrors sportlab's dev init (docker/dev/postgres-init/20_sportlab.sh):
#   zonedose_migrator  owns the database and schemas, runs Orchard setup/migrations
#   zonedose_app       reads and writes data only, never DDL, not table owner (RLS applies)
set -eu

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  -v app_password="$ZONEDOSE_APP_PASSWORD" \
  -v migrator_password="$ZONEDOSE_MIGRATOR_PASSWORD" <<'SQL'
CREATE ROLE zonedose_migrator LOGIN PASSWORD :'migrator_password';
CREATE ROLE zonedose_app LOGIN PASSWORD :'app_password';

CREATE DATABASE zonedose_spike OWNER zonedose_migrator;
REVOKE ALL ON DATABASE zonedose_spike FROM PUBLIC;
GRANT CONNECT ON DATABASE zonedose_spike TO zonedose_app;

\connect zonedose_spike

-- PostGIS needs a superuser: enabled here, not in a migration
CREATE EXTENSION IF NOT EXISTS postgis;

ALTER SCHEMA public OWNER TO zonedose_migrator;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO zonedose_app;

-- Tenants use table prefixes in public, not schemas: YesSql runs
-- CREATE SCHEMA IF NOT EXISTS at every start, which the app role may not do
-- (spike/results/README.md, finding 3)

ALTER DEFAULT PRIVILEGES FOR ROLE zonedose_migrator
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO zonedose_app;
ALTER DEFAULT PRIVILEGES FOR ROLE zonedose_migrator
  GRANT USAGE, SELECT ON SEQUENCES TO zonedose_app;
SQL
