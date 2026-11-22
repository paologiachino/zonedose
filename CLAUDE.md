# CLAUDE.md — zonedose

Working rules for Claude Code in this repository. The owner is Paolo, who validates every change and also uses the project to learn: explain non-obvious choices and proceed in small steps.

## Project

Zonedose is a platform built on Orchard Core (headless) that hosts several sites as tenants. The first is the Zonedose sports site (formerly Sportlab, still in the `sportlab` repository until the cut-over); a blog and a recipes site come later. The migration plan is in `docs/orchard-migration-plan.md`, decisions in `docs/decisions/`. Current phase: phase 1, repository skeleton. Next: phase 2, platform base (tenant hostnames, migrate step, app role).

`spike/` is the throwaway phase 0 spike: reference only, not built by CI, never extended.

## Stack (confirmed)

- Orchard Core 3.0 on .NET 10, PostgreSQL with PostGIS (one database, tenants by table prefix).
- Frontend: the Angular application in the `sportlab` repository, unchanged for now (phase 4).
- Containers: Docker, deployed on the homelab from phase 6.

## Working method

1. Always work on a branch (`feature/...`, `fix/...`, `chore/...`), never directly on `main` or `staging`. Pull requests go to `staging`; when it works, `staging` is merged into `main` with a new pull request. Both branches are protected by rulesets (required checks; `main` also needs an approval).
2. Small steps: one function or module at a time, with tests, then a pull request.
3. Briefly explain every non-obvious choice (Orchard features, recipes, migrations, module structure) so Paolo can validate it and learn.
4. Do not call a piece of work finished without running tests and style checks. If you cannot run them, say so.
5. One pull request at a time, described in English, with what changes and how to test it (one command per code block: zsh does not treat inline `#` as a comment).

## Conventions

- Chat with Paolo is in Italian. Code, identifiers, workflows, comments, documentation, commit messages and pull request descriptions are in English.
- Commit messages in English with a prefix: `feat:`, `fix:`, `chore:`, `docs:`, `test:`.
- C#: `dotnet format` with `.editorconfig`; warnings are errors; nullable enabled.
- Package versions only in `Directory.Packages.props`. A vulnerable package (direct or transitive) fails the restore: raise it there with a comment naming the advisory.

## Orchard rules

- Configuration as code: tenants come from `appsettings.json` and AutoSetup, features and settings from recipes in `src/Modules/*/Recipes`. No manual setup screens, no features enabled by hand outside development.
- Content types can be modelled in the admin of a local instance, then exported with a deployment plan (definition steps only, never settings with secrets or users) into the tenant recipe.
- Tenants use table prefixes, never schemas (decision 0002). Migrations run with the migrator role; the application role never needs DDL.
- Personal data never goes in content items: it lives in module-owned tables with `user_id` and Row-Level Security (decision 0002).

## Security and data rules

- No secrets in the code or in the repositories: everything comes from `ZONEDOSE_*` environment variables (see `.env.example`).
- Every piece of personal data has a `user_id`; every query goes through the layer that filters by the authenticated user. The database adds Row-Level Security as a second barrier.
- Users register with Google and stay "pending" until the administrator approves them. The administrator is impersonal: no name or email in the code or in recipes.
- Garmin keys are stored encrypted and never written to logs.
- Test data is synthetic only; never real health data in development or staging.

## Commands

No .NET SDK is needed on the Mac: everything runs in the official SDK image.

```sh
cp .env.example .env                 # once; replace every CHANGE_ME
docker compose up -d --wait db       # development PostGIS
docker compose up -d web             # Orchard on http://localhost:8080 (dotnet watch)
```

Build, style and tests in the SDK container (the tests need the database above):

```sh
docker run --rm --network zonedose-dev_default -v "$PWD":/src -w /src \
  -e "ZONEDOSE_TEST_DB_CONNECTION=Host=db;Database=zonedose_dev;Username=zonedose_dev_migrator;Password=<ZONEDOSE_MIGRATOR_PASSWORD>" \
  mcr.microsoft.com/dotnet/sdk:10.0 sh -c "dotnet format Zonedose.slnx --verify-no-changes && dotnet test --solution Zonedose.slnx"
```

Production image and its smoke test:

```sh
docker build -t zonedose:dev .
scripts/smoke-test-image.sh zonedose:dev zonedose-dev_default "Host=db;Database=<empty database>;Username=zonedose_dev_migrator;Password=<...>"
```

Database tests are skipped locally without `ZONEDOSE_TEST_DB_CONNECTION` but always run in CI.

## Licence

AGPL-3.0. New dependencies must have compatible licences.
