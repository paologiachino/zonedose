# Operations: database, release and backup

What an environment (staging, production) needs to run Zonedose, and the procedures the infrastructure repository implements in phase 6. Development does the same through `docker-compose.yml`.

## Database

One PostgreSQL database per environment in the existing `sportlab-postgres` container (PostGIS 17), with two roles (decision 0002). Every tenant lives in the same database, separated by table prefix (`host_...`, `zonedose_...`); there is nothing per tenant to create.

| Environment | Database | Migrator role (owner) | App role (data only) |
| --- | --- | --- | --- |
| Development | `zonedose_dev` | `zonedose_dev_migrator` | `zonedose_dev_app` |
| Staging | `zonedose_staging` | `zonedose_staging_migrator` | `zonedose_staging_app` |
| Production | `zonedose_prod` | `zonedose_prod_migrator` | `zonedose_prod_app` |

The SQL is the one in `docker/dev/postgres-init/20_zonedose.sh` with the environment name in place of `dev`, run once as superuser: the migrator owns the database and the `public` schema, PostGIS is enabled by the superuser, the app role gets data rights on everything the migrator creates (default privileges) and nothing else. The migrator does not need `CREATEDB` outside development.

## Configuration

| Variable | Used by | Value |
| --- | --- | --- |
| `ZONEDOSE_DB_MIGRATION_CONNECTION` | migrate step | migrator role connection string |
| `ZONEDOSE_SETUP_ADMIN_PASSWORD` | migrate step | password of the local `setup-admin` created in each new tenant |
| `ZONEDOSE_DB_CONNECTION` | application | app role connection string |
| `ZONEDOSE_HOST_<TENANT>` | both | site hostname of a tenant (`DEFAULT`, `ZONEDOSE`) |
| `ZONEDOSE_ADMIN_HOST_<TENANT>` | both | admin hostname of a tenant |
| `ZONEDOSE_GOOGLE_CLIENT_ID`, `ZONEDOSE_GOOGLE_CLIENT_SECRET` | application | Google OAuth client of the environment ([google-oauth.md](google-oauth.md)) |
| `ZONEDOSE_ADMIN_EMAIL` | application | the Google account created with this verified email is an active administrator |

Both containers must get the same hostnames. The admin of a tenant answers only on its admin hostname (404 elsewhere); the `Default` tenant is the platform administration, so its site and admin host are the same. The admin hostnames should be routed only on the internal network (LAN or Tailscale), never through the public tunnel.

The App_Data volume (`/app-data` in the image) is mounted by both containers.

## Release procedure

1. Production only: back up (below) before anything else.
2. Run the new image once with `migrate` and the migrator connection. It sets up any new tenant, enables on existing tenants the features their setup recipe lists, applies every pending migration, writes the configured hostnames into App_Data, and exits with 0. Any other exit code stops the release: the running version is untouched.
3. Replace the application container with the new image, app role connection.
4. Check `GET /api/health` on every site host: `200 {"status":"ok"}`. A `503 migrations-pending` means step 2 did not run for this version; roll back.

Rollback: start the previous image tag. If its migrations are incompatible with the database left by the new version, restore the backup taken in step 1 (database and App_Data together).

`scripts/smoke-test-image.sh` runs exactly these steps against a throwaway database, in CI and locally.

## Backup

Two things, always taken and restored together:

- **The database** (`pg_dump` of `zonedose_<env>`): every tenant's content, users and settings.
- **The App_Data volume**: `tenants.json` (tenants and hostnames), per-tenant settings, **per-tenant data protection keys** and media files. Without the keys, sign-in cookies are invalidated and every secret Orchard stored encrypted (for example the Google client secret, phase 3) can no longer be read.

The existing homelab backup (restic) must include both. A restore is tested once on staging before production goes live.
