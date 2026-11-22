# Frontend

Angular application with TypeScript and Tailwind CSS that talks to the backend only through the API. Plan of the first step: [docs/frontend-step-1.md](../docs/frontend-step-1.md).

Status: frontend step 1 done: sign-in with Google, waiting pages for pending and disabled accounts, user administration for administrators, silent session renewal; placeholder home page.

## Requirements

- Node.js 26 (the version is in `.nvmrc`; with nvm, `nvm use` in this folder).
- For pages that call the API: the Zonedose backend (Orchard Core) running on `http://localhost:8080`, from the [zonedose](https://github.com/paologiachino/zonedose) repository (`docker compose up -d web` there). The FastAPI backend in `backend/` is paused during the migration (phase 4 of the Zonedose plan); to use it instead, point `proxy.conf.json` back to `http://localhost:8000`.

## Commands

Run from this folder.

```sh
npm ci                  # dependencies, exactly as in package-lock.json
npm start               # development server on http://localhost:4200
npm test                # unit tests (Vitest), in watch mode in a terminal
npm test -- --watch=false   # unit tests once, as CI runs them
npm run lint            # ESLint, required before a pull request
npm run format:check    # Prettier check; npm run format fixes the files
npm run build           # production build in dist/sportlab
```

## How it is built

- **Angular 22**, created with the Angular CLI: standalone components (no `NgModule`), signals for state, zoneless change detection, strict TypeScript and strict templates.
- **Tailwind CSS 4** through PostCSS (`.postcssrc.json`, `src/styles.css`).
- **Unit tests** with Vitest through `ng test`; `*.spec.ts` files sit next to the code they test.
- **ESLint** with `angular-eslint` (`eslint.config.js`) and **Prettier** (`.prettierrc`).

## Same address as the API

`npm start` forwards every `/api/...` request to the Zonedose backend on `http://localhost:8080` (`proxy.conf.json`). The browser sees one site, `http://localhost:4200`, so the backend's `SameSite=Strict` session cookie works without CORS. In staging and production the reverse proxy does the same job.

The proxy keeps the browser's `Host` header (`changeOrigin: false`): the backend builds the Google redirect URI from it, so Google sends the browser back to `http://localhost:4200/api/auth/google/callback`, which the proxy forwards to the backend. Register that URI in the development Google client and set the client in the zonedose `.env` (see `docs/google-oauth.md` in the zonedose repository).

## Structure

```
src/app/
  app.ts, app.routes.ts, app.config.ts   root component, routes, application providers
  core/       session, interceptors (CSRF header, silent refresh), route guards, confirmation
  features/   one folder per area (auth, home, shell, admin), each loaded only when its route opens
  shared/     small reusable components (logout button)
src/testing/  test helpers: synthetic users and a fake session
```

## Planned pages

- Sign-in and onboarding (Google login, "pending approval" state).
- Private dashboard (HRV, training readiness, VO2max, training status, FTP, endurance score, sleep, heart rate).
- Activity list and search by sport type, with a detail view per sport.
- Map with contour lines and different styles per sport (MapLibre GL).
- Cycling segments and comparison between activities.
- Awards, achievements, badges and goals.
- Profile and settings (Garmin key, Instagram and Komoot accounts, token for Claude).
- Administration: approval of registrations.

The interface text is in Italian for now; translations will use Angular's built-in i18n when the site becomes public.
