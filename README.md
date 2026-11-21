# Zonedose

A multi-site platform built on [Orchard Core](https://orchardcore.net/) (headless, .NET 10) with PostgreSQL/PostGIS. Each site is an Orchard tenant: the first is the Zonedose sports site (activities and health data from Garmin, formerly Sportlab); a blog and a recipes site come later.

> Status: phase 1 of the [migration plan](docs/orchard-migration-plan.md), repository skeleton. Orchard sets up its tenants from configuration and recipes; there are no features yet beyond the platform base. Authentication arrives in phase 3.

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

| Tenant | Recipe | Development host |
| --- | --- | --- |
| `Default` | `ZonedoseHost`: hosts and administers the other tenants | `admin.localhost` |
| `zonedose` | `Zonedose`: the sports site | `localhost` |

Hostnames per environment come from `ZONEDOSE_HOST_<TENANT>` (phase 2). A host that matches no tenant gets 404.

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

The first start restores packages and compiles (a few minutes). The first request to each tenant runs its setup and returns an empty page: open `http://admin.localhost:8080/api/health` first (it also creates the other tenants), then `http://localhost:8080/api/health`, and reload each until it shows `{"status":"ok",...}`. The admin of each tenant is at `/Admin`, with the user `setup-admin` and `ZONEDOSE_SETUP_ADMIN_PASSWORD`.

Tests, style checks and the image build are described in [CLAUDE.md](CLAUDE.md#commands).

## Continuous integration

Every pull request runs: structure check, NuGet dependency audit (a vulnerable package fails the restore), `dotnet format`, build with warnings as errors, integration tests against PostGIS, the Docker image build with a smoke test, and a gitleaks secret scan. The same checks run weekly to catch newly published vulnerabilities.

## Licence

[AGPL-3.0](LICENSE).
