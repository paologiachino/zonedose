#!/bin/sh
# Deploys the production image as a release would and checks it:
#   1. migrate step with the migrator role (setup, migrations, hostnames)
#   2. the application with the app role
#   3. hosts, admin hostname, unknown host, non-root user, database role in use
#   4. a hostname change applied by a second migrate step
#
# Usage: scripts/smoke-test-image.sh <image> <docker network> <db container>
# Environment: SMOKE_DB_HOST (as seen from the network), SMOKE_DATABASE (an empty
# database owned by the migrator role), ZONEDOSE_MIGRATOR_PASSWORD, ZONEDOSE_APP_PASSWORD.
set -eu
IMAGE=$1 NETWORK=$2 DB_CONTAINER=$3
DB="Host=$SMOKE_DB_HOST;Database=$SMOKE_DATABASE"
MIGRATION="$DB;Username=zonedose_dev_migrator;Password=$ZONEDOSE_MIGRATOR_PASSWORD"
APP="$DB;Username=zonedose_dev_app;Password=$ZONEDOSE_APP_PASSWORD"
RUN=zonedose-smoke-$$
VOLUME=$RUN-appdata
SETUP_PASSWORD="Smoke-$(date +%s)!"
URL=http://127.0.0.1:18080
status=0
pass() { echo "PASS  $1"; }
fail() { echo "FAIL  $1"; status=1; }
check() { if [ "$2" = "$3" ]; then pass "$1"; else fail "$1 (expected $2, got $3)"; fi; }
code() { curl -s -o /dev/null -w '%{http_code}' -m 30 -H "Host: $1" "$URL$2"; }

cleanup() {
  docker logs "$RUN" 2>&1 | grep -E "fail:|crit:" -A5 | head -40 || true
  docker rm -f "$RUN" >/dev/null 2>&1 || true
  docker volume rm "$VOLUME" >/dev/null 2>&1 || true
}
trap cleanup EXIT

migrate() {
  docker run --rm --network "$NETWORK" -v "$VOLUME":/app-data \
    -e "ZONEDOSE_DB_MIGRATION_CONNECTION=$MIGRATION" \
    -e "ZONEDOSE_SETUP_ADMIN_PASSWORD=$SETUP_PASSWORD" "$@" \
    "$IMAGE" migrate
}

start_app() {
  docker rm -f "$RUN" >/dev/null 2>&1 || true
  docker run -d --name "$RUN" --network "$NETWORK" -v "$VOLUME":/app-data \
    -p 127.0.0.1:18080:8080 -e "ZONEDOSE_DB_CONNECTION=$APP" "$@" "$IMAGE" >/dev/null
  i=0
  until [ "$(code platform.localhost /api/health)" = 200 ]; do
    i=$((i + 1)); [ $i -gt 60 ] && { fail "application did not become ready"; exit 1; }
    sleep 2
  done
}

health() { curl -s -m 30 -H "Host: $1" "$URL/api/health"; }

# 1. Migrate step
if migrate | tail -3; then pass "migrate step"; else fail "migrate step"; exit 1; fi

# 2. Application with the app role
start_app
check "platform host -> Default" '{"status":"ok","tenant":"Default"}' "$(health platform.localhost)"
check "site host -> zonedose" '{"status":"ok","tenant":"zonedose"}' "$(health localhost)"
check "admin host -> zonedose" '{"status":"ok","tenant":"zonedose"}' "$(health admin.localhost)"
check "unknown host" 404 "$(code unknown.example.test /api/health)"
check "admin on the site host" 404 "$(code localhost /Admin)"
check "admin on the admin host" 302 "$(code admin.localhost /Admin)"
if [ "$(docker exec "$RUN" id -u)" != 0 ]; then pass "runs as non-root"; else fail "runs as root"; fi
roles=$(docker exec "$DB_CONTAINER" psql -U postgres -At -c \
  "SELECT string_agg(DISTINCT usename, ',') FROM pg_stat_activity WHERE datname = '$SMOKE_DATABASE' AND usename LIKE 'zonedose%'")
check "database role in use" zonedose_dev_app "$roles"

# 3. Hostname change through the migrate step
if migrate -e ZONEDOSE_ADMIN_HOST_ZONEDOSE=admin2.localhost | tail -1; then pass "migrate step with a new admin host"; else fail "migrate step with a new admin host"; fi
start_app -e ZONEDOSE_ADMIN_HOST_ZONEDOSE=admin2.localhost
check "new admin host" 302 "$(code admin2.localhost /Admin)"
check "old admin host" 404 "$(code admin.localhost /api/health)"

exit $status
