# ADR 0001 — Layering & dependency rule

Status: accepted

## Context
Receipts is a small expense-splitting API. We want a structure that demonstrates
Clean Architecture without over-engineering a CRUD app.

## Decision
Adopt the Clean Architecture dependency rule: dependencies point **inward only**.

```
Receipts.API  ->  Receipts.Application  ->  Receipts.Core (domain)
Receipts.Infrastructure  ->  Receipts.Application + Receipts.Core
```

- **Receipts.Core** — entities + pure domain logic (e.g. `SettlementCalculator`). No external dependencies, no EF, no ASP.NET.
- **Receipts.Application** — use cases + *port* interfaces (`IExpenseRepository`, `ISettlementService`). Depends only on Core.
- **Receipts.Infrastructure** — adapters: EF Core `DbContext`, repository implementations, JWT. Depends on Application + Core.
- **Receipts.API** — HTTP endpoints, DI composition root, DTO mapping.

Layers are introduced **only when a test requires the seam** (e.g. faking a
repository), never speculatively. Today only `Core`, `API`, and `Tests` exist;
`Application`/`Infrastructure` are added when persistence lands.

## Consequences
- Domain logic is unit-testable with zero setup (no DB/HTTP).
- Persistence is swappable behind interfaces (DIP) — EF in prod, fakes/SQLite in tests.
- Cost: more projects than a single-assembly app. Justified by testability and clarity.
