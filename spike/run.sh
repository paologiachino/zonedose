#!/bin/sh
# Container entrypoint: builds the tenant connection strings for DB_ROLE
# (migrator during setup and migrations, app for normal running) and starts Orchard.
set -eu
case "$DB_ROLE" in
  migrator) PASS="$ZONEDOSE_MIGRATOR_PASSWORD" ;;
  app) PASS="$ZONEDOSE_APP_PASSWORD" ;;
  *) echo "DB_ROLE must be migrator or app" >&2; exit 1 ;;
esac
CS="Host=db;Database=zonedose_spike;Username=zonedose_$DB_ROLE;Password=$PASS"

i=0
for tenant in Default zonedose blog; do
  # Used by auto-setup on first start
  export "OrchardCore__OrchardCore_AutoSetup__Tenants__${i}__DatabaseConnectionString=$CS"
  export "OrchardCore__OrchardCore_AutoSetup__Tenants__${i}__AdminPassword=$ORCHARD_ADMIN_PASSWORD"
  # Overrides the connection string stored in App_Data for an existing tenant
  export "OrchardCore__${tenant}__ConnectionString=$CS"
  i=$((i + 1))
done
export OrchardCore__ZonedoseSpike__AdminEmail="$ADMIN_EMAIL"
export OrchardCore__ZonedoseSpike__GoogleClientId="$GOOGLE_CLIENT_ID"
export OrchardCore__ZonedoseSpike__GoogleClientSecret="$GOOGLE_CLIENT_SECRET"

cd /src/src/Zonedose.Web
exec dotnet run --urls http://0.0.0.0:8000
