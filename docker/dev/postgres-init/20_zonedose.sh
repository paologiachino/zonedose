#!/bin/sh
# Development database: zonedose_dev and its two roles (decision 0002).
# Run once by the postgres image, as superuser, when the data volume is empty
# (after the image's own 10_postgis.sh). CI runs it too, on its service container.
#
#   zonedose_dev_migrator  owns the database, runs setup and migrations
#   zonedose_dev_app       reads and writes data only (used from phase 2)
set -eu

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  -v app_password="$ZONEDOSE_APP_PASSWORD" \
  -v migrator_password="$ZONEDOSE_MIGRATOR_PASSWORD" <<'SQL'
CREATE ROLE zonedose_dev_migrator LOGIN PASSWORD :'migrator_password';
CREATE ROLE zonedose_dev_app LOGIN PASSWORD :'app_password';

CREATE DATABASE zonedose_dev OWNER zonedose_dev_migrator;
REVOKE ALL ON DATABASE zonedose_dev FROM PUBLIC;
GRANT CONNECT ON DATABASE zonedose_dev TO zonedose_dev_app;

\connect zonedose_dev

-- PostGIS needs a superuser, so it is enabled here rather than in a migration
CREATE EXTENSION IF NOT EXISTS postgis;

-- Tenants use table prefixes in public, never schemas (decision 0002)
ALTER SCHEMA public OWNER TO zonedose_dev_migrator;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO zonedose_dev_app;

ALTER DEFAULT PRIVILEGES FOR ROLE zonedose_dev_migrator IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO zonedose_dev_app;
ALTER DEFAULT PRIVILEGES FOR ROLE zonedose_dev_migrator IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO zonedose_dev_app;
SQL
