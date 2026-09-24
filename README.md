# Natoshare

Natoshare is a personal budgeting and accounting app. You set a fixed income, it gets
split into categories automatically, you log your spending, and you can see what you
saved every month, all without Natoshare ever holding your real money. It works with
any currency, not just Naira.

This repo has the backend API (.NET) and two frontend apps (Next.js): the app your
users open, and the internal admin app the Natoshare team uses.

The full plan for this product, including the domain model, the API, and the design
system, is written up in the `docs/` folder (kept outside git, ask a teammate for a
copy if you don't have one).

## What's in this repo

```
src/Natoshare.Domain          business rules and types (Money, and so on)
src/Natoshare.Application     use-cases, sits between Domain and Infrastructure
src/Natoshare.Infrastructure  the database (EF Core + Postgres)
src/Natoshare.Api             the ASP.NET Core Web API
tests/                        one test project per layer above, plus tests/e2e
                               (a real Playwright suite against the whole stack)

web/user-app                  the app your users open (Next.js)
web/admin-app                 the internal admin app (Next.js)
web/shared-types              TypeScript types shared by both frontend apps
```

## Before you start

You need:

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (see `global.json` for the exact
  version)
- [Node.js 24+](https://nodejs.org/) and npm
- A Postgres database you can reach. Natoshare does not manage Postgres for you (not
  even with Docker, see below), it expects a real one, whether that is Postgres
  installed directly on your machine, or a managed cloud instance.
- [Docker](https://www.docker.com/), only if you want to run the API in a container
  (see "With Docker" below). It is not required for a plain `dotnet run`, though the
  backend's integration tests do need it either way (see "Running the backend tests").

## Setting up your `.env` (do this first, either way)

Both ways of running the backend below read the same root-level `.env` file:

```bash
cp .env.example .env
```

Then open `.env` and fill in:

- `DB_HOST` / `DB_PORT` / `DB_NAME` / `DB_USER` / `DB_PASSWORD` — your Postgres. If
  Postgres runs on this same machine, `DB_HOST=localhost` is right, the same as it
  would be for any other local process (see `.env.example`'s own comments for why).
- `JWT_SIGNING_KEY` — any random string, at least 32 characters. For example:
  ```bash
  python3 -c "import secrets; print(secrets.token_hex(32))"
  ```
- `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD` — the super-admin login the API creates
  automatically the first time it starts against an empty database (Development
  only, see `DevDataSeeder`). It also creates a `demo@natoshare.dev` account with a
  real onboarded budget (categories, income, a few expenses already logged), so you
  have something to actually look at right away, not just a bare login. Look at
  `DevDataSeeder.cs` if you ever need to change that demo password.

`.env` is gitignored, it never gets committed. Never put real production secrets in
it, this is a local development convenience only.

## Running the backend

Pick **one** of these, not both at once: the Docker container binds directly to
port 5001 on your machine (it uses host networking, see `docker-compose.yml`), so
it and a local `dotnet run` cannot both be up at the same time, whichever started
second will fail with "address already in use". If that happens, either stop the
other one (`docker compose down`, or `Ctrl+C` the `dotnet run`) or check nothing
was left running from an earlier session (`docker ps`). Both read the same `.env`
you just set up:

### With Docker

```bash
docker compose up
```

This builds and starts the API only (there is no Postgres service in
`docker-compose.yml`, see "Before you start"). The `api` service uses host
networking (Linux only), so it reaches a Postgres running on this same machine the
same way any other local process would.

### Without Docker

```bash
dotnet run --project src/Natoshare.Api
```

`Program.cs` reads the same root `.env` file directly (see `InsertRootDotEnvFallback`
in `Program.cs` if you want the details), so this needs no extra setup beyond the
`.env` file above.

### Either way

The API ends up on `http://localhost:5001`, with the Swagger docs at
`http://localhost:5001/swagger`, and a health check at `http://localhost:5001/health`.
In Development, it brings its own database schema up to date automatically on
startup, you do not need to run a migration by hand.

## Running the backend tests

```bash
dotnet test Natoshare.slnx
```

The integration tests spin up a real, throwaway Postgres in Docker for each run
(using Testcontainers, completely separate from the `.env`-configured one above), so
Docker needs to be running for them, whichever way you chose to run the API itself.
If they fail with a Docker "authentication required" error, or something about Ryuk
(the container cleanup helper), your `~/.docker/config.json` most likely has a stale
or broken login in it, nothing to do with the tests themselves. Work around it
without touching your real Docker login:

```bash
DOCKER_CONFIG=$(mktemp -d) TESTCONTAINERS_RYUK_DISABLED=true dotnet test Natoshare.slnx
```

## Running the frontend apps

The two apps and the shared types package are set up as one npm workspace, so you only
install once from the repo root:

```bash
npm install
```

Then, from the repo root:

```bash
npm run dev:user     # user-app on http://localhost:3000
npm run dev:admin    # admin-app on http://localhost:3001
```

Each app also has its own `.env.example`, copy it to `.env.local` inside that app's
folder and fill in your own values before you start it (the default already points at
`http://localhost:5001/api/v1`, matching the backend above).

Log into `admin-app` with the `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD` from your
root `.env`. Log into `user-app` with either that same account, or the seeded
`demo@natoshare.dev` account (see "Setting up your `.env`" above) if you want to look
around a budget that already has real data in it.

To lint or build a single app from the root:

```bash
npm run lint:user
npm run build:user
npm run lint:admin
npm run build:admin
```

## Running the end-to-end suite

`tests/e2e` is a real Playwright suite (the main user flow, and an admin smoke test)
run against the whole stack: the backend (either way, above) and both frontends
already running. See `tests/e2e/README.md` for the exact steps, or just:

```bash
npm run e2e
```

once everything above is up.
