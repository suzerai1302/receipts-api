# Receipts API

A clean, tested REST API for splitting shared expenses and settling up — "who owes whom" across a group, minimized to the fewest transactions. Built with ASP.NET Core and Entity Framework Core.

> **Live demo:** _coming soon_ · **Interactive API docs:** `/scalar` on the live URL

![CI](https://github.com/suzerai1302/receipts-api/actions/workflows/ci.yml/badge.svg)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Why this project

Splitting bills in a group (dinner, trip, rent) produces a tangle of "A paid for B, B paid for C…". This API records expenses and computes the **minimum set of payments** that settles everyone up — netting out circular debt so nobody makes more transfers than necessary.

## Features

- **JWT authentication** — register / login, BCrypt-hashed passwords
- **Groups & members** — create a group, add people by email
- **Expenses** — record who paid and who shared each cost (equal split)
- **Settlement engine** — nets all balances and returns the fewest debtor→creditor payments
- **Interactive docs** — OpenAPI + Scalar UI
- **Fully tested** — integration tests over real HTTP against the EF Core stack

## Tech stack

| Concern | Choice |
|---|---|
| Framework | ASP.NET Core (.NET 10), Minimal APIs |
| Persistence | Entity Framework Core + PostgreSQL |
| Auth | JWT bearer, BCrypt password hashing |
| Docs | OpenAPI + Scalar |
| Tests | xUnit + `WebApplicationFactory`, SQLite in-memory |
| CI | GitHub Actions |

## Architecture

Clean Architecture — dependencies point inward only ([ADR](docs/adr/0001-layering.md)):

```
Receipts.API            HTTP endpoints, DI composition, JWT, adapters (BCrypt, JWT)
   │
Receipts.Infrastructure EF Core DbContext + repository implementations
   │
Receipts.Core           entities, repository ports, SettlementCalculator (pure, zero deps)
```

The domain (`Receipts.Core`) has no framework dependencies — the settlement algorithm is a pure function, unit-tested in isolation. Persistence sits behind repository interfaces (`IUserRepository`, `IGroupRepository`), so the in-memory store used while prototyping was swapped for EF Core **without touching a single endpoint** (Dependency Inversion).

## API

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/auth/register` | – | Create an account |
| POST | `/auth/login` | – | Get a JWT |
| POST | `/groups` | ✓ | Create a group (creator becomes a member) |
| POST | `/groups/{id}/members` | ✓ | Add a member by email |
| POST | `/groups/{id}/expenses` | ✓ | Record an expense (payer, amount, participants) |
| GET | `/groups/{id}/settlement` | ✓ | Computed "who owes whom" |
| GET | `/health` | – | Liveness check |

## Running locally

Requires the .NET 10 SDK and a PostgreSQL instance.

```bash
# set your connection string (or edit appsettings.json)
export ConnectionStrings__Postgres="Host=localhost;Database=receipts;Username=postgres;Password=postgres"

dotnet run --project src/Receipts.API
# browse http://localhost:5xxx/scalar for interactive docs
```

Migrations are applied automatically on startup.

## Testing

```bash
dotnet test
```

Integration tests boot the real app via `WebApplicationFactory` against a SQLite in-memory database — exercising the full HTTP → EF Core path.

## Deployment

Containerized via the included `Dockerfile`. Deployable to any container host (Render, Railway, Fly.io). Set two environment variables:

- `ConnectionStrings__Postgres` — your Postgres connection string
- `Jwt__Key` — a secret signing key (≥ 32 chars)

> The committed `appsettings.json` ships throwaway local-dev defaults. Production reads the two variables above — never commit real secrets.

## License

[MIT](LICENSE) © Giovani Ladores
