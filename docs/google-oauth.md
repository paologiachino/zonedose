# Google sign-in: OAuth client setup

Zonedose signs people in with Google. The application needs an OAuth client created in Google Cloud: its **client ID** and **client secret** go into `ZONEDOSE_GOOGLE_CLIENT_ID` and `ZONEDOSE_GOOGLE_CLIENT_SECRET`. This is done once per environment, by hand, in the Google Cloud console. The flow in the code is described in [decision 0001](decisions/0001-auth-model.md).

The Sportlab project already has a Google Cloud project with clients: its development client already accepts `http://localhost:4200/api/auth/google/callback`, so it works for Zonedose as is. Create new clients the same way when needed (Sportlab's `docs/google-oauth.md` describes the console step by step: Branding, Audience, Data access, Clients).

## Clients and redirect URIs

One OAuth client per environment, of type **Web application**, with no JavaScript origins (the browser never talks to Google with the client ID; the exchange happens on the server).

| Environment | Authorized redirect URI |
| --- | --- |
| Development, through the Angular server (the usual way) | `http://localhost:4200/api/auth/google/callback` |
| Development, Orchard alone | `http://localhost:8080/api/auth/google/callback` |
| Staging, production | `https://<site host>/api/auth/google/callback`, hosts chosen in phase 6 |

The redirect URI is built from the host the browser used, so it must match the console **exactly**: scheme, host, port, path, no trailing slash. Behind the reverse proxy (phase 6) the forwarded host and scheme must reach the application.

Scopes: `openid`, `email` and `profile` (Google's defaults for this client), nothing else. From Google the application keeps only the account id (`sub`), the verified email address and the display name; it stores no Google token.

## Configure

In `.env` at the repository root (never committed):

```sh
ZONEDOSE_GOOGLE_CLIENT_ID=<client ID of the development client>
ZONEDOSE_GOOGLE_CLIENT_SECRET=<its secret>
ZONEDOSE_ADMIN_EMAIL=<the Google address that becomes the first administrator>
```

Then restart the application:

```sh
docker compose up -d --force-recreate web
```

Then start the Angular frontend from the `sportlab` repository (`cd frontend && npm start`) and open `http://localhost:4200`. Its development server forwards `/api` to Orchard on port 8080 and keeps the browser's host, so Orchard builds the redirect URI `http://localhost:4200/api/auth/google/callback` and Google sends the browser back through the Angular server.

Without the frontend, `http://localhost:8080/api/auth/google/login` works too if the `8080` redirect URI is registered; after Google the browser lands on `http://localhost:8080/`, a 404 since the site host serves the API only, but `http://localhost:8080/api/auth/me` then answers with the account.

Without a client configured, `/api/auth/google/login` redirects to `/login?error=unavailable`. The automated tests never call Google: they use a fake Google (`tests/Zonedose.Web.Tests/FakeGoogle.cs`).
