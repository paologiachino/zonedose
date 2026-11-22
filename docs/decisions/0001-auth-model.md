# 0001: Authentication model

Status: accepted (phase 0, 2026-11-21); implementation details below added in phase 3 (2026-11-22)

## Context

Zonedose (formerly Sportlab) has a working Angular frontend built on same-site HttpOnly cookies, a fixed `/api/auth/*` contract and an `X-Requested-With` CSRF header. Orchard tenants have separate users. The owner decided that each site has its own login (no shared login across sites) and that the platform may become a public site in the future.

## Decision

Per-tenant ASP.NET Identity cookie, no identity tenant and no OpenID Connect server for now.

- Each tenant (Zonedose, later blog and recipes) has its own users and its own sign-in with Google, through Orchard's Google and external authentication features.
- A small module keeps the existing contract: `GET /api/auth/me`, `POST /api/auth/refresh` (204 while the cookie is valid), `POST /api/auth/logout`, `GET /api/auth/google/login`. The Angular session service, interceptors and guards stay as they are.
- CSRF: SameSite cookie plus the `X-Requested-With` header on unsafe `/api/` requests, enforced by a middleware in the module. Orchard's antiforgery tokens stay for its own Razor pages.
- Account states: `disabled` is `IsEnabled = false`; `pending` and `active` are a status on an enabled user. Orchard's built-in moderation is not used, because it blocks pending users from signing in and shows Orchard's pages.

## Consequences

- No frontend change for authentication (verified in the spike).
- Phase 3 must still add, with tests: `SameSite=Strict`, `Secure`, cookie lifetimes equivalent to today, security stamp update when a user is disabled, `email_verified` check, Google errors redirected to the Angular login page, last-admin guard, and a decision on the local super user that setup creates.
- If shared login across sites is wanted later, an identity tenant with Orchard's OpenID server can be added in front; the per-site contract would then sit on tokens instead of the cookie. Not needed now.
- Running at work or publicly later does not change this: it is standard ASP.NET Identity with an external provider.

Evidence: [spike results](../../spike/README.md), findings 1, 2 and 5 to 8.

## Implementation (phase 3)

What phase 3 built, and where it differs from Sportlab:

- **Google** is registered by the `Zonedose.Accounts` module from configuration (`ZONEDOSE_GOOGLE_*`), not through Orchard's Google feature: no secret in the database, and the whole flow is ours. ASP.NET's Google handler runs the authorization code flow with PKCE and `state`; the redirect URI stays `/api/auth/google/callback`, as in Sportlab. A verified email is required; every failure ends on `/login?error=<code>`.
- **Identity** is the Google `sub` (an Orchard external login). Orchard user names are generated (`user-…`), never the email, so no log line carries personal data (tested).
- **Sessions are server-side.** The cookie holds a random key; the session is a row in a module-owned table, which stores only the key's SHA-256 hash. 30 days sliding and 90 days at most from the sign-in, as in Sportlab. Instead of a 15-minute access token, every request checks the session on the server and reads status and role from the database, so logout and disable take effect at once, a copied cookie included.
- **Cookie**: `__Host-zonedose_session`, HttpOnly, Secure, `SameSite=Strict`, path `/` (development: `zonedose_session` without Secure). CSRF: `X-Requested-With` on every state-changing API request, as in Sportlab.
- **Disabled accounts** are Orchard's `IsEnabled = false`: Orchard itself refuses them everywhere, the admin included. Unlike Sportlab, a disabled person cannot sign in to see an "access removed" page: the sign-in ends on `/login?error=disabled`, which the frontend shows (phase 4).
- **The site host serves the API only.** Orchard's own pages (admin, local sign-in) answer only on the admin host, which is internal. There, the local `setup-admin` created by setup remains as the emergency account; Zonedose administrators manage users through the API.
- **Last-admin guard**: administration changes run under Orchard's distributed lock and save before releasing it (in-process lock on the single homelab instance; several instances would need Redis).
