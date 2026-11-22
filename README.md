# Zonedose

A multi-site platform built on [Orchard Core](https://orchardcore.net/) (headless, .NET 10) with PostgreSQL/PostGIS. Each site is an Orchard tenant: the first is the Zonedose sports site (activities and health data from Garmin, formerly Sportlab); a blog and a recipes site come later.

> Status: phase 2 of the [migration plan](docs/orchard-migration-plan.md), platform base: tenants from configuration and recipes, a separate migrate step with the migrator role, the application on the data-only app role, and each tenant's admin on its own hostname. Authentication arrives in phase 3.

## Layout

| Path | What |
| --- | --- |
| `src/Zonedose.Web` | Orchard host. Tenant layout in `appsettings.json`; secrets and hostnames from `ZONEDOSE_*` variables. |
| `src/Modules/Zonedose.Platform` | Shared module: `/api/health` and the setup recipes (`zonedose-host`, `zonedose`). |
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

## Migrate step and roles

The application never changes the database schema. Before each release, `Zonedose.Web migrate` runs once with the migrator role: it sets up new tenants, applies pending migrations, writes the configured hostnames, and exits with an error if anything is left pending. The application then runs with the data-only app role, and `/api/health` answers `503 migrations-pending` if a migration was skipped. Database, release and backup requirements for each environment are in [docs/operations.md](docs/operations.md).

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

Tests, style checks and the image build are described in [CLAUDE.md](CLAUDE.md#commands).

## Continuous integration

Every pull request runs: structure check, NuGet dependency audit (a vulnerable package fails the restore), `dotnet format`, build with warnings as errors, integration tests against PostGIS (migrate step with the migrator role, then the application with the app role), the Docker image build with a smoke test of the whole release procedure, and a gitleaks secret scan. The same checks run weekly to catch newly published vulnerabilities.

## Branches

Work happens on `feature/...`, `fix/...` or `chore/...` branches and reaches `staging` through a pull request; `staging` reaches `main` through another pull request. Both are protected by GitHub rulesets: the four CI checks must pass, `main` also needs an approval, and force pushes and deletion are blocked. Deploys from these branches arrive in phase 6.

## Licence

[AGPL-3.0](LICENSE).
