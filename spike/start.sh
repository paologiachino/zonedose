#!/bin/sh
# Usage: ./start.sh migrator|app
# (Re)starts Orchard with the given database role and waits until every
# tenant answers. migrator: setup and migrations. app: normal running.
set -eu
cd "$(dirname "$0")"
case "${1:-}" in
  migrator|app) ;;
  *) echo "usage: $0 migrator|app" >&2; exit 2 ;;
esac
DB_ROLE=$1 docker compose up -d --force-recreate web
echo "Waiting for Orchard (first start compiles and restores, a few minutes)..."
i=0
until [ "$(curl -s -o /dev/null -w '%{http_code}' -m 5 -H 'Host: admin.localhost' http://127.0.0.1:8000/)" != "000" ]; do
  i=$((i + 1)); [ $i -gt 120 ] && { echo "Orchard did not start; see: docker compose logs web" >&2; exit 1; }
  sleep 5
done
failed=0
# The first request to each tenant runs its setup or pending migrations
for host in admin.localhost localhost blog.localhost; do
  code=$(curl -s -o /dev/null -w '%{http_code}' -m 300 -H "Host: $host" http://127.0.0.1:8000/)
  echo "$host $code"
  case "$code" in 5*) failed=1 ;; esac
done
[ $failed -eq 0 ] || { echo "A tenant failed; see: docker compose logs web" >&2; exit 1; }
echo "Running with DB_ROLE=$1"
