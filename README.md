# Frontend

Angular application with TypeScript and Tailwind CSS that talks to the backend only through the API. Plan of the first step: [docs/frontend-step-1.md](../docs/frontend-step-1.md).

Status: session handling, sign-in page, waiting pages for pending and disabled accounts, placeholder home page.

## Requirements

- Node.js 26 (the version is in `.nvmrc`; with nvm, `nvm use` in this folder).
- For pages that call the API: the backend running on `http://localhost:8000` (see [backend/README.md](../backend/README.md)).

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

`npm start` forwards every `/api/...` request to the backend on `http://localhost:8000` (`proxy.conf.json`). The browser sees one site, `http://localhost:4200`, so the backend's `SameSite=Strict` session cookies work without CORS. In staging and production the reverse proxy does the same job.

To sign in through the frontend, the backend must know that the browser uses `http://localhost:4200`: set `PUBLIC_URL=http://localhost:4200` in `.env` (or in the shell that starts the backend), and register `http://localhost:4200/api/auth/google/callback` in the development Google client. Google then sends the browser back to the Angular server, which forwards the callback to the backend.

## Structure

```
src/app/
  app.ts, app.routes.ts, app.config.ts   root component, routes, application providers
  core/       session, API access, interceptors, guards (from the next pull requests)
  features/   one folder per area, each loaded only when its route opens
  shared/     small reusable components
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
