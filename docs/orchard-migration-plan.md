# Zonedose: Orchard Core + Angular platform, migration plan

Status: phase 0 done (2026-11-21), decision: go. Results in [spike/README.md](../spike/README.md), decisions in [docs/decisions](decisions/).

Naming: the sports site formerly called Sportlab is now **Zonedose** (name and available domain). In this plan "Zonedose" is the site and the platform; the existing repositories and containers (`sportlab`, `sportlab-infra`, `sportlab-postgres`) keep their names until phase 6/7.

## 1. Goal

One reusable platform (Orchard Core headless + Angular) that hosts several sites as tenants: Zonedose first, then a blog and a recipes site. Shared: authentication, users, roles, media, deploy pipeline. Zonedose-specific: the Garmin module.

Secondary goal: evaluate Orchard Core on a real project, with a view to using it at work.

## 2. What exists today (read from the repositories)

- `sportlab`: FastAPI + SQLAlchemy + Alembic backend, PostgreSQL/PostGIS, Angular frontend (standalone components, signals, zoneless, Tailwind, Vitest). Done: Google sign-in (Authlib), own rotating refresh tokens in HttpOnly cookies, CSRF header, user approval (`pending`/`active`/`disabled`), `ADMIN_EMAIL` rule, last-admin guard, admin page, 14+ backend test files, Bruno collection, CI.
- Not started: Garmin keys and sync (backend step 3), MCP server, Row-Level Security policies, the staging deploy.
- `sportlab-infra`: dedicated `sportlab-postgres` container (PostGIS 17), separate migrator and app roles per environment, staging/production compose files and deploy workflows (drafts, untested), homelab runner.

Consequence: no Garmin data and no production data exist yet, so there is nothing to migrate except synthetic staging users. This is the cheapest moment to change backend.

## 3. Feasibility assessment

Verdict: feasible, with three structural conflicts to resolve in the spike. It is a good fit for the multi-site platform goal, a weaker fit for Zonedose alone.

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
- **Go/no-go gate.** If conflicts 1 or 2 cannot be solved cleanly, stop: keep FastAPI for Zonedose and consider Orchard only for blog and recipes.

### Phase 1: `zonedose` repository skeleton

Status: done (2026-11-21). Not yet in place: branch protection on `main` (a manual GitHub setting).

- Orchard solution (.NET 10), Dockerfile, CI (build, tests, format, dependency and secret scan), `CLAUDE.md` with the same rules as the other repositories, AGPL-3.0.
- Orchard configuration as code (recipes), no manual setup steps.
- Content types: modelled in the admin of a local instance, exported with a deployment plan (definition steps only) and committed to the tenant recipe; staging and production get them only from the recipe (workflow verified in the spike).

### Phase 2: platform base

- Tenants: `zonedose` (one login per site, [decision 0001](decisions/0001-auth-model.md)); hostnames per environment.
- PostgreSQL in `sportlab-postgres` with the agreed role model ([decision 0002](decisions/0002-database-role-model.md): table prefixes, separate migrate step); backup procedure extended to the Orchard databases.
- Admin UI on a separate hostname.

### Phase 3: authentication parity

Reproduce, with tests, what Sportlab has today (gaps listed in the spike findings 5 to 8): Google-only sign-in, pending/active/disabled, roles, `ADMIN_EMAIL` only at creation, last-admin guard, CSRF protection, session lifetimes, no personal data in logs.

### Phase 4: Angular adaptation

- Point the session service, interceptors and guards at the new endpoints; admin users page on the new API.
- Keep the frontend in the `sportlab` repository for now (rename to Zonedose there in phase 7); extract shared libraries (session, CSRF, API client) into a monorepo only when the second site needs them.

### Phase 5: Garmin module (the original backend step 3)

- Module-owned tables with `user_id` and RLS; Garmin keys encrypted; activity summaries and tracks (PostGIS), comparison endpoints.
- Sync: a .NET background task in the module; a separate Python worker writing to the tables under RLS only if Garmin access needs a Python-only library ([decision 0003](decisions/0003-garmin-sync-placement.md)). Note: no Python sync exists yet.

### Phase 6: infrastructure

- `sportlab-infra`: Orchard service in the staging and production compose files, migrate step, image tags, deploy and rollback procedure, backup before production migrations. Deploy to staging first, with synthetic data only.

### Phase 7: cut-over and new sites

- Parity checklist signed off on staging, then retire the FastAPI backend (keep its branch tagged for a while); rename the Sportlab repositories, containers and branding to Zonedose.
- Blog and recipes as new tenants.

## 5. Order relative to the current roadmap

The current roadmap is: staging deploy, then Garmin sync. Recommended: do phase 0 now (small, throwaway). Deploy the current stack to staging only if phase 0 is delayed; otherwise build the staging deploy once, for the stack that will remain.

## 6. Owner's answers (2026-11-21)

1. The current FastAPI stack can pause during the migration: no real users yet.
2. One login per site is acceptable: no shared login, no identity tenant for now.
3. The platform may become a public site in the future: keep standard, portable choices (ASP.NET Identity with Google; an OpenID server can be added later if needed).

## 7. Phase 0 outcome: go

Both blocking conflicts are solved cleanly in the spike:

- Conflict 1 (database roles): Orchard runs entirely with the data-only app role, provided tenants use table prefixes instead of schemas and migrations run in a separate step with the migrator role.
- Conflict 2 (RLS): personal data in module-owned tables with `user_id` and RLS works from Orchard with PostGIS; as the app role a user sees only their rows.
- Conflict 3 (authentication): per-site Identity cookie behind the existing `/api/auth/*` contract; the Angular app works unchanged.

Not verified in the spike: a real Google sign-in round trip (needs a person with a Google account; the redirect to Google works). Work moved to phase 3: cookie lifetimes and flags, ending sessions of disabled users, `email_verified`, Google errors to the Angular login page, the local super user created by setup. Phase 1 must also handle the vulnerable transitive packages of Orchard 3.0.1.
