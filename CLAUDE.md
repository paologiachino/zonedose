# CLAUDE.md — zonedose

Working rules for Claude Code in this repository. The owner is Paolo, who validates every change and also uses the project to learn: explain non-obvious choices and proceed in small steps.

## Project

Zonedose is a platform built on Orchard Core (headless) that hosts several sites as tenants. The first is the Zonedose sports site. Zonedose replaces Sportlab entirely: the Angular frontend moved here (`frontend/`, with its history), the backend is rebuilt on Orchard Core, and the `sportlab` repository is archived at the cut-over (phase 7); no new work goes there. A blog and a recipes site come later. The migration plan is in `docs/orchard-migration-plan.md`, decisions in `docs/decisions/`. Current phase: phase 4 done (the Angular frontend moved into `frontend/` and runs on this API). Next: phase 5, the Garmin module.

`spike/` is the throwaway phase 0 spike: reference only, not built by CI, never extended.

## Stack (confirmed)

- Orchard Core 3.0 on .NET 10, PostgreSQL with PostGIS (one database, tenants by table prefix).
- Frontend: Angular, TypeScript, Tailwind CSS, in `frontend/` (its own README).
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
- TypeScript: ESLint and Prettier (`npm run lint`, `npm run format:check` in `frontend/`); unit tests with Vitest, never calling the backend.
- Package versions only in `Directory.Packages.props`. A vulnerable package (direct or transitive) fails the restore: raise it there with a comment naming the advisory.

## Orchard rules

- Configuration as code: tenants come from `appsettings.json` and AutoSetup, features and settings from recipes in `src/Modules/*/Recipes`. No manual setup screens, no features enabled by hand outside development. A feature added to a setup recipe reaches existing tenants through the migrate step (enable only; disabling a feature is an explicit decision).
- Content types can be modelled in the admin of a local instance, then exported with a deployment plan (definition steps only, never settings with secrets or users) into the tenant recipe.
- Tenants use table prefixes, never schemas (decision 0002). Migrations run only in the migrate step (`Zonedose.Web migrate`) with the migrator role; the application runs with the app role and never needs DDL. Release and backup procedures: `docs/operations.md`.
- A tenant's site host serves `/api` only; Orchard's pages (admin, local sign-in) answer only on its admin hostname (`ZONEDOSE_ADMIN_HOST_<TENANT>`).
- Every API endpoint has a request in `bruno/`; `BrunoCollectionTests` fails otherwise. Tests never call Google: they use `FakeGoogle`.
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
docker compose up -d web             # migrate step, then Orchard with the app role on :8080
docker compose up -d --force-recreate migrate web   # after changes with new migrations or features
```

Build, style and tests in the SDK container (the tests need the database above):

```sh
docker run --rm --network zonedose-dev_default -v "$PWD":/src -w /src \
  -e "ZONEDOSE_TEST_DB_CONNECTION=Host=db;Database=zonedose_dev;Username=zonedose_dev_migrator;Password=<ZONEDOSE_MIGRATOR_PASSWORD>" \
  -e "ZONEDOSE_TEST_DB_APP_CONNECTION=Host=db;Database=zonedose_dev;Username=zonedose_dev_app;Password=<ZONEDOSE_APP_PASSWORD>" \
  mcr.microsoft.com/dotnet/sdk:10.0 sh -c "dotnet format Zonedose.slnx --verify-no-changes && dotnet test --solution Zonedose.slnx"
```

Frontend (Node.js from `frontend/.nvmrc`), with Orchard running:

```sh
cd frontend
npm ci
npm start                            # http://localhost:4200, /api forwarded to Orchard on :8080
npm run lint && npm run format:check
npm test -- --watch=false
npm run build
```

Production image and its smoke test:

```sh
docker build -t zonedose:dev .
SMOKE_DB_HOST=db SMOKE_DATABASE=<empty database prepared like zonedose_dev> \
  ZONEDOSE_MIGRATOR_PASSWORD=<...> ZONEDOSE_APP_PASSWORD=<...> \
  scripts/smoke-test-image.sh zonedose:dev zonedose-dev_default zonedose-dev-db-1
```

Database tests are skipped locally without the two test connections but always run in CI.

## Licence

AGPL-3.0. New dependencies must have compatible licences.
