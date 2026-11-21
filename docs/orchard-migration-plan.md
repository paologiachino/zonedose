# Zonedose: Orchard Core + Angular platform, migration plan for Sportlab

Status: draft plan for review. Nothing here has been implemented or tested. Statements about Orchard Core come from its public documentation and must be confirmed by the spike (phase 0).

## 1. Goal

One reusable platform (Orchard Core headless + Angular) that hosts several sites as tenants: Sportlab first, then a blog and a recipes site. Shared: authentication, users, roles, media, deploy pipeline. Sportlab-specific: the Garmin module.

Secondary goal: evaluate Orchard Core on a real project, with a view to using it at work.

## 2. What exists today (read from the repositories)

- `sportlab`: FastAPI + SQLAlchemy + Alembic backend, PostgreSQL/PostGIS, Angular frontend (standalone components, signals, zoneless, Tailwind, Vitest). Done: Google sign-in (Authlib), own rotating refresh tokens in HttpOnly cookies, CSRF header, user approval (`pending`/`active`/`disabled`), `ADMIN_EMAIL` rule, last-admin guard, admin page, 14+ backend test files, Bruno collection, CI.
- Not started: Garmin keys and sync (backend step 3), MCP server, Row-Level Security policies, the staging deploy.
- `sportlab-infra`: dedicated `sportlab-postgres` container (PostGIS 17), separate migrator and app roles per environment, staging/production compose files and deploy workflows (drafts, untested), homelab runner.

Consequence: no Garmin data and no production data exist yet, so there is nothing to migrate except synthetic staging users. This is the cheapest moment to change backend.

## 3. Feasibility assessment

Verdict: feasible, with three structural conflicts to resolve in the spike. It is a good fit for the multi-site platform goal, a weaker fit for Sportlab alone.

| Area | Assessment |
| --- | --- |
| Headless + Angular | Supported: Orchard Core 3.0 (June 2026, .NET 10) exposes GraphQL and can host custom controllers/endpoints. |
| Multi-tenancy | Native, the strongest reason to adopt it. Tenants are isolated (own users, DB or prefix, themes, content types). |
| Shared login across sites | Not automatic: tenants have separate users. Needs one identity tenant acting as OpenID Connect server, or an external IdP. Decision in phase 0. |
| Google sign-in + approval | Orchard has Google authentication and registration settings; whether "pending until an admin approves" can be reproduced as is must be verified. |
| Garmin module | Fine as a custom module (content parts, background task, endpoints). Garmin client libraries are mostly Python: keep the existing Python sync as a separate worker, or reimplement in .NET. Decision in phase 5. |
| Licence | Orchard Core is BSD-3-Clause, compatible with the project's AGPL-3.0. |

Conflicts to resolve:

1. **Database roles.** Today the app role cannot change the schema and does not own tables (so RLS applies), while the migrator role owns it. Orchard creates and migrates its tables at startup with the connection it is given. Options: run Orchard once with the migrator connection (a "migrate" step like today's job), then run it with the app role; or give up the split for Orchard's own tables only. Personal data tables stay under the stricter model.
2. **Row-Level Security.** Orchard stores content as JSON documents (YesSql) plus index tables. RLS on those is awkward. Personal data (activities, tracks, Garmin keys) should live in module-owned relational tables, with `user_id` and RLS as today, not in content items.
3. **Authentication model.** The current Angular session code assumes same-site HttpOnly cookies and `/api/auth/*` endpoints. Orchard's natural options are the ASP.NET Identity cookie, or tokens through its OpenID module. Same-site cookie keeps the frontend closest to today; tokens fit a shared IdP better. Decision in phase 0.

Other risks: one shared instance means shared downtime and shared upgrades (Orchard majors can break modules); a .NET stack is new on the homelab (images, runner, monitoring); the Orchard admin UI is a second attack surface to protect (separate hostname, strong auth).

## 4. Plan

Each phase ends with a pull request or a short decision record, in small steps, with tests, as in the existing repositories.

### Phase 0: decisions and spike (1 to 2 days, throwaway)

- Spike in a scratch branch: Orchard 3.0 on .NET 10 with PostgreSQL, a tenant with Google login, one secured custom endpoint, Angular calling it with the existing session service.
- Verify: approval flow, tenant isolation of users, behaviour with the migrator/app role split, PostGIS access from a module (Npgsql + NetTopologySuite), RLS on a module-owned table.
- Write three short decision records: auth model, database/role model, Garmin sync placement.
- **Go/no-go gate.** If conflicts 1 or 2 cannot be solved cleanly, stop: keep FastAPI for Sportlab and consider Orchard only for blog and recipes.

### Phase 1: `zonedose` repository skeleton

- Orchard solution (.NET 10), Dockerfile, CI (build, tests, format, dependency and secret scan), `CLAUDE.md` with the same rules as the other repositories, AGPL-3.0.
- Orchard configuration as code (recipes), no manual setup steps.

### Phase 2: platform base

- Tenants: `identity` (or external IdP, per decision), `sportlab`; hostnames per environment.
- PostgreSQL in `sportlab-postgres` with the agreed role model; backup procedure extended to the Orchard databases.
- Admin UI on a separate hostname.

### Phase 3: authentication parity

Reproduce, with tests, what Sportlab has today: Google-only sign-in, pending/active/disabled, roles, `ADMIN_EMAIL` only at creation, last-admin guard, CSRF protection, session lifetimes, no personal data in logs.

### Phase 4: Angular adaptation

- Point the session service, interceptors and guards at the new endpoints; admin users page on the new API.
- Keep the Sportlab frontend in its repository for now; extract shared libraries (session, CSRF, API client) into a monorepo only when the second site needs them.

### Phase 5: Garmin module (the original backend step 3)

- Module-owned tables with `user_id` and RLS; Garmin keys encrypted; activity summaries and tracks (PostGIS), comparison endpoints.
- Sync: either the existing Python worker writing through the module API, or a .NET background task (decided in phase 0).

### Phase 6: infrastructure

- `sportlab-infra`: Orchard service in the staging and production compose files, migrate step, image tags, deploy and rollback procedure, backup before production migrations. Deploy to staging first, with synthetic data only.

### Phase 7: cut-over and new sites

- Parity checklist signed off on staging, then retire the FastAPI backend (keep its branch tagged for a while).
- Blog and recipes as new tenants.

## 5. Order relative to the current roadmap

The current roadmap is: staging deploy, then Garmin sync. Recommended: do phase 0 now (small, throwaway). Deploy the current stack to staging only if phase 0 is delayed; otherwise build the staging deploy once, for the stack that will remain.

## 6. Open questions for the owner

1. Is Sportlab required to keep working on the current stack while the migration happens, or can it pause?
2. Shared login across all sites desired, or one login per site acceptable?
3. Hosting at work in mind: would the platform ever run outside the homelab (affects the IdP choice)?
