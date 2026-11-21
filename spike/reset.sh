#!/bin/sh
# Throw everything away and set up again from configuration (spike only).
set -eu
cd "$(dirname "$0")"
docker compose down -v
rm -rf src/Zonedose.Web/App_Data
docker compose up -d
until [ "$(curl -s -o /dev/null -w '%{http_code}' -m 5 -H 'Host: admin.localhost' http://127.0.0.1:8000/)" != "000" ]; do sleep 5; done
for host in admin.localhost localhost blog.localhost; do
  curl -s -o /dev/null -w "$host %{http_code}\n" -m 300 -H "Host: $host" http://127.0.0.1:8000/
done
