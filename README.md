# Zonedose

Zonedose is a multi-user web site for sports activities and health data. Each person connects their Garmin account; activities, tracks and health metrics are downloaded into a private database and shown with dashboards, maps and goals. The same data is available to Claude through an MCP server, for questions, suggestions and training planning.

It is built for personal and family use on a homelab, and designed so that it can become a public site later.

> Status: phase 4 of the [migration plan](docs/orchard-migration-plan.md) done: the Angular frontend (in the `sportlab` repository) runs on this backend, with Google sign-in, account approval, server-side sessions and user administration. Next: phase 5, the Garmin module.

## What it does

The goal of the project; most of it is still to build (see the roadmap below).

- **Activities and health data from Garmin**: runs, rides, hikes and their tracks, plus health metrics, synchronised per person (from phase 5).
- **Maps**: tracks on MapLibre GL maps with contour lines from elevation data, stored and queried with PostGIS.
- **Dashboards and goals**: trends over time, comparisons between activities, personal goals.
- **Access for Claude**: a read-only MCP server on top of the same API, so a person can ask an assistant about their own data.
- **Private by design**: sign-in with Google only, and every new account stays pending until an administrator approves it. Each person sees only their own data, enforced twice: by the application and by Row-Level Security in PostgreSQL. Garmin keys are stored encrypted, and development and staging use synthetic data only.

## One platform, several sites

Zonedose runs on [Orchard Core](https://orchardcore.net/) (headless, .NET 10) with PostgreSQL/PostGIS. Each site is an Orchard tenant with its own users, content and administration, sharing one deployment, one database and the same release procedure:

| Site | Tenant | Status |
| --- | --- | --- |
| Zonedose, the sports site | `zonedose` | in progress |
| Platform administration | `Default` | in place |
| Blog | to come | phase 7 |
| Recipes | to come | phase 7 |

The project was born as Sportlab (FastAPI and Angular, in the `sportlab` repository). This repository is its new home: the Angular frontend is reused, and the backend moves to Orchard Core step by step, following the [migration plan](docs/orchard-migration-plan.md) and the [decision records](docs/decisions/).

## Stack

| Layer | Technology |
| --- | --- |
| Platform and API | Orchard Core 3.0 on .NET 10, custom modules |
| Database | PostgreSQL with PostGIS (one database, tenants by table prefix) |
| Frontend | Angular, TypeScript, Tailwind CSS (in the [`sportlab`](https://github.com/paologiachino/sportlab) repository for now) |
| Maps | MapLibre GL |
| Access for Claude | MCP server (read-only at first) |
| Containers and CI | Docker, GitHub Actions |

## Roadmap

| Phase | What | Status |
| --- | --- | --- |
| 0 | Orchard spike and decisions | done |
| 1 | Repository skeleton, CI, production image | done |
| 2 | Platform base: migrate step, roles, admin hostnames | done |
| 3 | Authentication: Google, approval, roles, sessions | done |
| 4 | Angular frontend on the new API | done |
| 5 | Garmin module: keys, sync, tracks, comparisons | next |
| 6 | Staging and production on the homelab | |
| 7 | Cut-over from Sportlab, then blog and recipes | |

## Layout

| Path | What |
| --- | --- |
| `src/Zonedose.Web` | Orchard host. Tenant layout in `appsettings.json`; secrets and hostnames from `ZONEDOSE_*` variables. |
| `src/Modules/Zonedose.Platform` | Shared module: `/api/health`, CSRF check, API-only site hosts, and the setup recipes (`zonedose-host`, `zonedose`). |
| `src/Modules/Zonedose.Accounts` | Google sign-in, account approval, server-side sessions, `/api/auth/*` and `/api/admin/users`. |
| `bruno/` | API requests for [Bruno](https://www.usebruno.com/), kept in sync with the code by a test. |
| `tests/Zonedose.Web.Tests` | Integration tests: the real application set up against PostgreSQL. |
| `docker/dev/postgres-init` | Development database and its migrator/app roles. |
| `Dockerfile`, `scripts/smoke-test-image.sh` | Production image (non-root) and its smoke test. |
| `docs/` | Migration plan and decision records. |
| `spike/` | Phase 0 spike, kept for reference. |

## Tenants

| Tenant | Recipe | Site host (dev) | Admin host (dev) |
| --- | --- | --- | --- |
| `Default` | `ZonedoseHost`: hosts and administers the other tenants | `platform.localhost` | `platform.localhost` |
| `zonedose` | `Zonedose`: the sports site | `localhost` | `admin.localhost` |

Hostnames per environment come from `ZONEDOSE_HOST_<TENANT>` and `ZONEDOSE_ADMIN_HOST_<TENANT>`. A tenant's admin answers only on its admin host; a host that matches no tenant gets 404.

## Sign-in and API

People sign in with Google only ([setup](docs/google-oauth.md)). A new account is pending until an administrator approves it; the account with the verified email `ZONEDOSE_ADMIN_EMAIL` is created as an active administrator. The API keeps Sportlab's contract, so the Angular frontend works unchanged:

| Endpoint | What |
| --- | --- |
| `GET /api/auth/google/login` | start the Google sign-in (browser) |
| `GET /api/auth/me` | the signed-in user |
| `POST /api/auth/refresh`, `POST /api/auth/logout` | session |
| `GET /api/admin/users`, `POST /api/admin/users/{id}/approve`, `/disable`, `/role` | user administration |

Sessions live on the server (30 days sliding, 90 at most); the cookie is HttpOnly and `SameSite=Strict`, and state-changing requests need `X-Requested-With`. The site host answers only under `/api`. Every endpoint has a request in the [Bruno collection](bruno/README.md).

## Migrate step and roles

The application never changes the database schema. Before each release, `Zonedose.Web migrate` runs once with the migrator role: it sets up new tenants, enables on existing tenants any feature their setup recipe lists (a recipe otherwise runs only when the tenant is created), applies pending migrations, writes the configured hostnames, and exits with an error if anything is left pending. The application then runs with the data-only app role, and `/api/health` answers `503 migrations-pending` if a migration was skipped. Database, release and backup requirements for each environment are in [docs/operations.md](docs/operations.md).

## Development

Requirements: Docker. The .NET SDK is not needed on the host.

```sh
cp .env.example .env
```

Replace every `CHANGE_ME` in `.env` with throwaway values, then:

```sh
docker compose up -d --wait db
```

```sh
docker compose up -d web
```

Compose runs the `migrate` service first (setup and migrations with the migrator role), then `web` with the app role. The first start restores packages and compiles (a few minutes). Then `http://platform.localhost:8080/api/health` and `http://localhost:8080/api/health` answer `{"status":"ok",...}`. The admin of the sports site is at `http://admin.localhost:8080/Admin`, the platform's at `http://platform.localhost:8080/Admin`, with the user `setup-admin` and `ZONEDOSE_SETUP_ADMIN_PASSWORD`. After pulling changes that add migrations or features, run `docker compose up -d --force-recreate migrate web`.

### With the frontend

The Angular application lives in the `sportlab` repository for now. With Orchard running as above:

```sh
cd <sportlab>/frontend && npm ci && npm start
```

Open `http://localhost:4200`. Its development server forwards `/api` to Orchard on port 8080 and keeps the browser's host, so the session cookie and the Google redirect work as in production. Google sign-in needs `ZONEDOSE_GOOGLE_CLIENT_ID`, `ZONEDOSE_GOOGLE_CLIENT_SECRET` and `ZONEDOSE_ADMIN_EMAIL` in `.env` ([setup](docs/google-oauth.md)).

Tests, style checks and the image build are described in [CLAUDE.md](CLAUDE.md#commands).

## Continuous integration

Every pull request runs: structure check, NuGet dependency audit (a vulnerable package fails the restore), `dotnet format`, build with warnings as errors, integration tests against PostGIS (migrate step with the migrator role, then the application with the app role), the Docker image build with a smoke test of the whole release procedure, and a gitleaks secret scan. The same checks run weekly to catch newly published vulnerabilities.

## Branches

Work happens on `feature/...`, `fix/...` or `chore/...` branches and reaches `staging` through a pull request; `staging` reaches `main` through another pull request. Both are protected by GitHub rulesets: the four CI checks must pass, `main` also needs an approval, and force pushes and deletion are blocked. Deploys from these branches arrive in phase 6.

## Licence

[AGPL-3.0](LICENSE).
