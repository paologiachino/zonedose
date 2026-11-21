#!/bin/sh
# Spike checks against the running stack. Usage: ./test.sh [suffix]
# Every user is synthetic (example.test); the admin is whatever ADMIN_EMAIL says.
set -u
cd "$(dirname "$0")"
S=${1:-}
B=http://127.0.0.1:8000; H="Host: localhost"; J='Content-Type: application/json'; X='X-Requested-With: XMLHttpRequest'
ADMIN=$(grep ^ADMIN_EMAIL .env | cut -d= -f2)
T=$(mktemp -d)
code() { curl -s -o /dev/null -w "%{http_code}" "$@"; }
reg() { curl -s -c "$T/$1" -H "$H" -H "$J" -H "$X" -X POST $B/api/auth/dev/register -d "{\"email\":\"$2\"}"; }
idof() { curl -s -b "$T/$1" -H "$H" $B/api/auth/me | sed 's/.*"id":"\([^"]*\)".*/\1/'; }
setstatus() { code -b "$T/admin" -H "$H" -H "$J" -H "$X" -X POST $B/api/admin/users/$1/status -d "{\"status\":\"$2\"}"; }
check() { if [ "$2" = "$3" ]; then echo "PASS  $1 ($3)"; else echo "FAIL  $1 (expected $2, got $3)"; fi; }

check "anonymous /api/auth/me" 401 "$(code -H "$H" $B/api/auth/me)"
check "POST without X-Requested-With" 403 "$(code -H "$H" -H "$J" -X POST $B/api/auth/dev/register -d '{"email":"x@example.test"}')"
reg alice "alice$S@example.test" >/dev/null
check "new user is pending" pending "$(curl -s -b "$T/alice" -H "$H" $B/api/auth/me | sed 's/.*"status":"\([a-z]*\)".*/\1/')"
check "pending user cannot read activities" 403 "$(code -b "$T/alice" -H "$H" $B/api/activities)"
reg admin "$ADMIN" >/dev/null 2>&1
[ -s "$T/admin" ] && grep -q orchauth "$T/admin" || curl -s -o /dev/null -c "$T/admin" -H "$H" -H "$J" -H "$X" -X POST $B/api/auth/dev/register -d "{\"email\":\"$ADMIN\"}"
check "ADMIN_EMAIL user is admin" admin "$(curl -s -b "$T/admin" -H "$H" $B/api/auth/me | sed 's/.*"role":"\([a-z]*\)".*/\1/')"
A=$(idof alice)
check "pending user cannot approve herself" 403 "$(code -b "$T/alice" -H "$H" -H "$J" -H "$X" -X POST $B/api/admin/users/$A/status -d '{"status":"active"}')"
check "admin approves alice" 200 "$(setstatus $A active)"
check "alice adds activity (PostGIS point)" 200 "$(code -b "$T/alice" -H "$H" -H "$J" -H "$X" -X POST $B/api/activities -d '{"name":"Aosta run","lon":7.32,"lat":45.737}')"
reg bob "bob$S@example.test" >/dev/null; Bo=$(idof bob); setstatus $Bo active >/dev/null
code -b "$T/bob" -H "$H" -H "$J" -H "$X" -X POST $B/api/activities -d '{"name":"Bob hike","lon":11.3,"lat":46.5}' >/dev/null
check "alice sees only her activities" 0 "$(curl -s -b "$T/alice" -H "$H" $B/api/activities | grep -c 'Bob hike')"
echo "INFO  alice list: $(curl -s -b "$T/alice" -H "$H" $B/api/activities | cut -c1-160)"
echo "INFO  rls probe:  $(curl -s -b "$T/alice" -H "$H" $B/api/activities/rls-probe)"
setstatus $Bo disabled >/dev/null
check "disabled user cannot read activities" 403 "$(code -b "$T/bob" -H "$H" $B/api/activities)"
echo "INFO  disabled user's existing cookie on /me: $(code -b "$T/bob" -H "$H" $B/api/auth/me)"
check "zonedose cookie not valid on blog tenant admin" 302 "$(code -b "$T/alice" -H "Host: blog.localhost" $B/Admin)"
check "refresh while signed in" 204 "$(code -b "$T/alice" -H "$H" -H "$X" -X POST $B/api/auth/refresh)"
curl -s -o /dev/null -b "$T/alice" -c "$T/alice-out" -H "$H" -H "$X" -X POST $B/api/auth/logout
check "me after logout" 401 "$(code -b "$T/alice-out" -H "$H" $B/api/auth/me)"
check "refresh after logout" 401 "$(code -b "$T/alice-out" -H "$H" -H "$X" -X POST $B/api/auth/refresh)"
check "Google login redirects to Google" accounts.google.com "$(curl -s -o /dev/null -w '%{redirect_url}' -H "$H" $B/api/auth/google/login | cut -d/ -f3)"
rm -rf "$T"
