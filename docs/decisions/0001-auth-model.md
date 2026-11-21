# 0001: Authentication model

Status: accepted (phase 0, 2026-11-21)

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
