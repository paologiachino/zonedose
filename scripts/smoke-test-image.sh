#!/bin/sh
# Starts the production image against a PostgreSQL and checks that both
# tenants are set up from configuration and answer on their host.
# Usage: scripts/smoke-test-image.sh <image> <docker network> <connection string>
# CI runs it after building the image; it also works against the dev database.
set -eu
IMAGE=$1 NETWORK=$2 CONNECTION=$3
NAME=zonedose-smoke-$$
docker run -d --name "$NAME" --network "$NETWORK" -p 127.0.0.1:18080:8080 \
  -e "ZONEDOSE_DB_CONNECTION=$CONNECTION" \
  -e "ZONEDOSE_SETUP_ADMIN_PASSWORD=Smoke-$(date +%s)!" \
  "$IMAGE" >/dev/null
trap 'docker logs "$NAME" 2>&1 | grep -E "fail:|crit:" -A5 | head -40; docker rm -f "$NAME" >/dev/null' EXIT

i=0
until [ "$(curl -s -o /dev/null -w '%{http_code}' -m 5 -H 'Host: admin.localhost' http://127.0.0.1:18080/)" != "000" ]; do
  i=$((i + 1)); [ $i -gt 60 ] && { echo "The container did not start"; exit 1; }
  sleep 2
done

status=0
for pair in admin.localhost:Default localhost:zonedose; do
  host=${pair%%:*} tenant=${pair#*:}
  # The first request runs the tenant setup
  curl -s -o /dev/null -m 300 -H "Host: $host" http://127.0.0.1:18080/
  body=$(curl -s -m 30 -H "Host: $host" http://127.0.0.1:18080/api/health)
  if [ "$body" = "{\"status\":\"ok\",\"tenant\":\"$tenant\"}" ]; then
    echo "PASS  $host -> $tenant"
  else
    echo "FAIL  $host: $body"; status=1
  fi
done
code=$(curl -s -o /dev/null -w '%{http_code}' -H 'Host: unknown.example.test' http://127.0.0.1:18080/api/health)
[ "$code" = 404 ] && echo "PASS  unknown host -> 404" || { echo "FAIL  unknown host -> $code"; status=1; }
[ "$(docker exec "$NAME" id -u)" != 0 ] && echo "PASS  runs as non-root" || { echo "FAIL  runs as root"; status=1; }
exit $status
