#!/bin/sh
# Throws everything away and sets up all tenants again from configuration,
# with the migrator role (spike only). Then: ./test.sh m1, ./start.sh app, ./test.sh a1
set -eu
cd "$(dirname "$0")"
docker compose down -v
rm -rf src/Zonedose.Web/App_Data
./start.sh migrator
