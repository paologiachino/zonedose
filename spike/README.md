# Phase 0 spike: Orchard Core 3.0 for Zonedose

Throwaway code for phase 0 of [the migration plan](../docs/orchard-migration-plan.md). It answers the plan's questions with running code; it is not the phase 1 skeleton. Decisions taken from it are in [docs/decisions](../docs/decisions/).

Run on 2026-11-21 with Orchard Core 3.0.1, .NET 10 (official `mcr.microsoft.com/dotnet/sdk:10.0` image, nothing installed on the host), `postgis/postgis:17-3.7`, and the unchanged Sportlab Angular frontend.

## What is here

| Path | What |
| --- | --- |
| `docker-compose.yml`, `run.sh` | PostGIS and Orchard. `DB_ROLE` in `.env` chooses the connection: `migrator` (setup, migrations) or `app` (normal running). |
| `db/10_zonedose.sh` | Database init: `zonedose_migrator` owns the database, `zonedose_app` has data rights only, PostGIS enabled by the superuser. Same model as Sportlab today. |
| `src/Zonedose.Web` | Orchard host. Tenants are set up from `appsettings.json` by AutoSetup: `Default` (SaaS, `admin.localhost`), `zonedose` (`localhost`), `blog` (Headless, `blog.localhost`). |
| `src/Zonedose.Spike` | The custom module: the `/api/auth/*` contract the Angular app already uses, the approval flow, the secured `/api/activities` endpoint, and a module-owned PostGIS table with RLS. |
| `test.sh` | The checks below, as curl calls. `results/` holds their output. |
| `angular-proxy.json` | Proxy for running the Sportlab frontend against Orchard (`ng serve --proxy-config`). |

## How to run

Once, from `spike/`: copy the example file, then replace every `CHANGE_ME` in `.env` (any throwaway values; Google keys are optional, only the real Google sign-in needs them).

```sh
cp .env.example .env
```

Then each step on its own. `reset.sh` deletes the spike database, sets up all tenants with the migrator role and waits until they answer (the first start restores packages and compiles, a few minutes).

```sh
./reset.sh
```

```sh
./test.sh m1
```

`start.sh` restarts Orchard with the given role; `app` is normal running. The test suffix (`m1`, `a1`) makes the synthetic users new on each run.

```sh
./start.sh app
```

```sh
./test.sh a1
```

The database role is chosen only by these scripts, never by `.env`. To see a migration applied, restart with `./start.sh migrator`, then `./start.sh app` again.

`/api/auth/dev/register` (Development only) stands in for the Google round trip: it calls the same `UserService.RegisterAsync` that Orchard's Google auto-registration calls, or signs in an existing user.

## Results

All checks pass with the app role ([results/run-app.txt](results/run-app.txt)) and after a migration applied by the migrator ([results/run-app-after-migrate.txt](results/run-app-after-migrate.txt)).

| Question from the plan | Result |
| --- | --- |
| Orchard 3.0 on .NET 10 with PostgreSQL | Works. Three tenants set up from configuration, no manual screens (needs `AddSetupFeatures("OrchardCore.AutoSetup")`). |
| Google login | Configured from environment variables (no secret in recipes); `/api/auth/google/login` redirects to Google with PKCE. **Not verified end to end**: completing it needs a person signing in with a real Google account. |
| Approval flow | Reproduced with a small handler: new users are `pending`, the `ADMIN_EMAIL` user is `active` + `Administrator`, a pending user gets 403 on data and on admin endpoints, an admin can approve. Orchard's own moderation is not usable as is (see finding 2). |
| Tenant isolation of users | Confirmed: users live in per-tenant tables (`zonedose_UserIndex`, `blog_UserIndex`), the cookie is per tenant (`orchauth_zonedose`), a Zonedose session is not valid on the blog tenant. |
| Migrator/app role split | Works with two conditions: tenants use table prefixes, not schemas (finding 3), and migrations run in a separate step with the migrator connection (finding 4). Orchard then runs entirely with the app role. |
| PostGIS from a module | Works: own `NpgsqlDataSource` with NetTopologySuite from the tenant connection string, `geometry(Point, 4326)` read and written as NTS `Point`, `ST_Distance` in queries. |
| RLS on a module-owned table | Works: as `zonedose_app`, a user sees 1 of 4 rows, and 0 with no user set. The table owner (migrator) bypasses RLS, as in Sportlab. |
| Content types from the admin GUI | Works with the app role: in the `zonedose` tenant admin, an `Article` type (Title, HtmlBody) was created and an item published, with no errors. The type definition is a YesSql document (no DDL); the item is in the existing `ContentItemIndex`. Enabling *features* from the admin is different: their migrations create tables, so features go in the recipe and run with the migrator (finding 4). |
| Angular with the existing session service | Works unchanged: login page on 401, pending page for a pending user, home page after approval, logout through the CSRF interceptor (204). |

## Findings

1. **The session contract can stay.** `/api/auth/me`, `/refresh`, `/logout`, `/google/login` are a thin controller over the ASP.NET Identity cookie. `refresh` becomes a no-op (204 while the cookie is valid) because the cookie renews itself; the Angular refresh interceptor does not notice.
2. **Orchard's moderation does not match Sportlab's UX.** With `UsersAreModerated`, a pending user is a disabled Orchard user, who cannot sign in at all and sees Orchard's Razor "registration pending" page. Sportlab signs pending users in so the Angular app shows its own waiting page. The spike keeps `pending`/`active` in the user's properties and uses `IsEnabled = false` only for `disabled`. Moderation also lives in the `Users.Registration` feature, which would expose a password sign-up form; the spike does not enable it.
3. **YesSql runs `CREATE SCHEMA IF NOT EXISTS` at every tenant start** when the tenant has a schema, and PostgreSQL refuses it to a role without `CREATE` on the database, even when the schema exists. With a schema per tenant the app role cannot start any tenant. With table prefixes in `public` it starts fine: at startup YesSql only reads, and creates its document and id tables only if they are missing.
4. **Pending migrations under the app role fail, but the tenant keeps running.** Orchard logs `must be owner of table` and serves requests with the old schema. So migrations must be a separate step with the migrator connection (as Sportlab's Alembic job), and the deploy needs a check that no migration is pending before switching traffic.
5. **Disabling a user does not end their session.** After `IsEnabled = false` the existing cookie still passes `/api/auth/me` (data endpoints return 403 because of the status check). Phase 3 must update the security stamp on disable and shorten the stamp validation interval.
6. **Cookie defaults differ from Sportlab.** `orchauth_zonedose` is a session cookie, `SameSite=Lax`, no `Secure` on plain HTTP. Sportlab uses `SameSite=Strict`, 15-minute access, 30-day refresh, 90-day maximum. To be set in phase 3.
7. **Orchard's Google callback ends on Orchard pages.** Errors redirect to Orchard's `/Login` Razor page instead of the Angular `/login?error=...`, and `email_verified` is not checked (Sportlab refuses unverified addresses because `ADMIN_EMAIL` could otherwise be claimed). Phase 3 needs its own callback handling or login events for both.
8. **Setup always creates a local super user with a password** in each tenant (`setup-admin` here). Phase 3 must decide how to neutralise it (disable it after setup, or keep it only for the admin hostname) since Sportlab is Google-only.
9. **The admin theme needs `OrchardCore.Navigation`**, or every admin page fails with "shape type 'Navigation' is not found". The spike recipe enables it with Contents, ContentTypes, Media, Markdown, Lists and Deployment (for exporting definitions to a recipe; the export itself was not tried). `UseExternalProviderIfOnlyOneDefined` is off so the local `setup-admin` can reach the admin; with it on, `/Login` goes straight to Google.
10. **Smaller notes.** `IGoogleAuthenticationService` is registered only as its concrete class (inject `GoogleAuthenticationService`). Orchard 3.0.1 brings transitive packages with known vulnerabilities (SixLabors.ImageSharp 3.1.11, HtmlSanitizer beta, System.Security.Cryptography.Pkcs, MySqlConnector, AngleSharp): the phase 1 dependency scan will flag them, so they need explicit version overrides or exceptions. Data protection keys must be persisted outside the container (the Google client secret is stored encrypted with them).
