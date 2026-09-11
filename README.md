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
tests/                        one test project per layer above

web/user-app                  the app your users open (Next.js)
web/admin-app                 the internal admin app (Next.js)
web/shared-types              TypeScript types shared by both frontend apps
```

## Before you start

You need:

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (see `global.json` for the exact
  version)
- [Node.js 24+](https://nodejs.org/) and npm
- [Docker](https://www.docker.com/) and Docker Compose, if you want to run Postgres in
  a container instead of installing it yourself

## Running the backend

The easiest way, with Docker, is to bring up Postgres and the API together:

```bash
docker compose up
```

The API will be on `http://localhost:5001`, with the Swagger docs at
`http://localhost:5001/swagger`, and a health check at `http://localhost:5001/health`.

If you would rather run the API yourself and only use Docker for the database:

```bash
docker compose up -d postgres
dotnet ef database update --project src/Natoshare.Infrastructure --startup-project src/Natoshare.Api
dotnet run --project src/Natoshare.Api
```

Note: the Postgres container is reachable from your own machine on port `5433`, not the
usual `5432`, because a lot of machines already have something else listening on
`5432`. The API itself always talks to Postgres over the Docker network, so this only
matters if you want to connect to the database yourself with a GUI or `psql`.

Run the backend tests with:

```bash
dotnet test Natoshare.slnx
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
folder and fill in your own values before you start it.

To lint or build a single app from the root:

```bash
npm run lint:user
npm run build:user
npm run lint:admin
npm run build:admin
```
