# 0003: Garmin sync placement

Status: proposed (phase 0, 2026-11-21); to confirm when Garmin API access is known

## Context

The plan assumed an existing Python sync to keep. There is none: Garmin keys and sync (Sportlab backend step 3) were never started. So the choice is not "keep or rewrite" but "where to write it". The spike showed that a module can own relational tables with PostGIS and RLS, and that Orchard runs with a data-only role.

## Decision

Write the sync as part of the Zonedose Garmin module in .NET (a background task plus the module's tables), **if** the access is through Garmin's official developer APIs (OAuth, REST, push notifications): those are language-neutral HTTP and fit a .NET background task and endpoint.

Fallback, only if access depends on a Python-only library (unofficial Garmin Connect clients): a separate Python worker, with its own data-only database role, writing to the module-owned tables directly with `zonedose.user_id` set per transaction, so RLS still applies. It would not go through the module's HTTP API (extra auth for little gain).

## Consequences

- One language and one deploy unit in the common case; Garmin keys encrypted with ASP.NET data protection, never logged.
- Before phase 5: confirm which Garmin API is available to the project, and its terms for multi-user use.
- The MCP server (not started) can read through the same module API later.
