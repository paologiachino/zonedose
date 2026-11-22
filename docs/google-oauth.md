# Google sign-in: OAuth client setup

Zonedose signs people in with Google. The application needs an OAuth client created in Google Cloud: its **client ID** and **client secret** go into `ZONEDOSE_GOOGLE_CLIENT_ID` and `ZONEDOSE_GOOGLE_CLIENT_SECRET`. This is done once per environment, by hand, in the Google Cloud console. The flow in the code is described in [decision 0001](decisions/0001-auth-model.md).

The Sportlab project already has a Google Cloud project with clients: reuse it and add the Zonedose redirect URIs below to the existing clients, or create new clients the same way (Sportlab's `docs/google-oauth.md` describes the console step by step: Branding, Audience, Data access, Clients).

## Clients and redirect URIs

One OAuth client per environment, of type **Web application**, with no JavaScript origins (the browser never talks to Google with the client ID; the exchange happens on the server).

| Environment | Authorized redirect URI |
| --- | --- |
| Development, Orchard alone | `http://localhost:8080/api/auth/google/callback` |
| Development, through the Angular server (phase 4) | `http://localhost:4200/api/auth/google/callback` |
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

Open `http://localhost:8080/api/auth/google/login`. After Google, the browser lands on `http://localhost:8080/` with the session cookie set; that page is a 404 until the frontend is served in front of the API (phase 4), but `http://localhost:8080/api/auth/me` then answers with the account.

Without a client configured, `/api/auth/google/login` redirects to `/login?error=unavailable`. The automated tests never call Google: they use a fake Google (`tests/Zonedose.Web.Tests/FakeGoogle.cs`).
