# Bruno API collection

Requests for exploring the Zonedose API by hand with [Bruno](https://www.usebruno.com/) (MIT licence). The collection is plain files in the repository, in the OpenCollection YAML format (Bruno 3.0 and later), so it is reviewed in pull requests like code. It keeps the layout and conventions of the Sportlab collection, since the API contract is the same.

Bruno is for manual exploration. The automated API tests are in `tests/Zonedose.Web.Tests`.

## Layout

```
bruno/
  opencollection.yml      collection root (name, default environment)
  environments/
    Local.yml             baseUrl: the zonedose site host in the development Compose
  health/
    health.yml            GET /api/health
  auth/                   Google sign-in and the session
    google-login.yml      GET /api/auth/google/login (redirect to Google)
    me.yml                GET /api/auth/me
    refresh.yml           POST /api/auth/refresh
    logout.yml            POST /api/auth/logout
  admin/                  user administration (admins only)
    list-users.yml        GET /api/admin/users
    approve.yml           POST /api/admin/users/:user_id/approve
    disable.yml           POST /api/admin/users/:user_id/disable
    set-role.yml          POST /api/admin/users/:user_id/role
```

The two ends of the Google redirect (`/api/auth/google/callback`, handled by ASP.NET, and `/api/auth/google/finish`) have no request: only the browser coming back from Google calls them.

## Setup

1. Start the development stack from the repository root (see the main README):

   ```sh
   docker compose up -d web
   ```

2. In Bruno: **Open Collection** and choose this `bruno/` folder. Bruno reads and writes the files in place; there is no import step.
3. Select the **Local** environment (top right) if it is not already selected.
4. Open a request and send it. The status assertion shows in the **Tests** tab of the response.

To run the whole collection from the terminal with the Bruno CLI (`npm install -g @usebruno/cli`):

```sh
cd bruno && bru run --env Local
```

## Request format

Requests are `.yml` files with `info`, `http`, `runtime.assertions`, `settings` and `docs`. A request without authentication has no `auth` field at all: in this format `auth` is either `inherit` or an object, and `auth: none` is rejected. State-changing requests carry the `X-Requested-With` header (CSRF barrier); without it the API answers `403`.

## Secrets

Never write a cookie, password or key into these files. A value Bruno must send goes into an environment variable marked as **secret**, or into its cookie manager: both stay on your machine, outside the repository.

## Using a browser session

Sign-in needs a browser (Google's pages), so Bruno borrows the session the browser opened. The session is one HttpOnly cookie, `zonedose_session` in development (`__Host-zonedose_session` with HTTPS in staging and production). The cookie holds only a key: the session itself is on the server.

1. Configure Google sign-in (see `docs/google-oauth.md`) and sign in at http://localhost:8080/api/auth/google/login.
2. In the browser's developer tools (**Application** or **Storage** → **Cookies** → `http://localhost:8080`) copy the value of `zonedose_session`.
3. In Bruno, open the **Cookies** manager and add `zonedose_session` with that value for the domain **`localhost`**, path `/`.
4. Send `auth/Me`: `200` with your account.

The session lasts 30 days from the last use and 90 days at most from the sign-in; `auth/Logout` ends it on the server, for the browser too. Treat a copied cookie like a password: it is a live session.

## Keeping the collection in sync with the API

`tests/Zonedose.Web.Tests/BrunoCollectionTests.cs` compares the API endpoints declared in the code (the controllers of the Zonedose modules, plus `/api/health`) with the requests in this folder. The test suite, and therefore CI, fails when:

- an endpoint has no request here: add one;
- a request points to an endpoint that no longer exists: update or remove it.

So every pull request that adds, renames or removes an endpoint also updates this folder. Endpoints only a browser reaches (marked `[ApiExplorerSettings(IgnoreApi = true)]`) are left out.

### Adding a request

- **In Bruno:** right-click the folder, **New Request**, fill in method and URL as `{{baseUrl}}/api/...`, then save. Bruno writes a new `.yml` file in the folder; commit it.
- **By hand:** copy an existing request file and change `info.name`, `info.seq`, `http.method`, `http.url` and `docs`.
- Path parameters use Bruno's `:name` form (`{{baseUrl}}/api/admin/users/:user_id/approve`); the test matches them to the route's `{userId}` by position.
- A new group of endpoints gets a new folder with its own `folder.yml`.
