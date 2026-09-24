# Natoshare end-to-end tests

Two Playwright specs, run against the real stack (API + Postgres + both
frontends), not against mocks:

- `specs/user-flow.spec.ts` — the main user journey: sign up, finish onboarding,
  land on a working dashboard.
- `specs/admin-flow.spec.ts` — an admin smoke test: log in, see the users list,
  open one account's detail, read the audit log.

## Running locally

1. Start the API and Postgres: `docker compose up -d` from the repo root.
2. Build and start both frontends (they need to be talking to that API):
   ```
   npm run build --workspace=user-app && npm run start --workspace=user-app -- -p 3000
   npm run build --workspace=admin-app && npm run start --workspace=admin-app -- -p 3001
   ```
3. From this folder: `npx playwright test` (or `npm run e2e` from the repo root).

`USER_APP_URL` / `ADMIN_APP_URL` override the default `localhost:3000` /
`localhost:3001` if you are pointing at something else. `E2E_ADMIN_EMAIL` /
`E2E_ADMIN_PASSWORD` override the admin login the admin-flow spec uses (defaults
match the account `DevDataSeeder` creates in a `Development` environment).

## In CI

See `.github/workflows/ci.yml`'s `e2e` job: it brings up `docker compose`, waits
for `/health`, builds and starts both frontends, then runs this suite exactly the
way described above.
