# SplitBook

A lightweight REST API for tracking shared group expenses. Think of it as the backend behind a Splitwise-style app: friends, roommates, or travel buddies record who paid for what, and the server keeps a running picture of who owes whom.

## What it does

- **Groups** — named collections (e.g. "Lisbon Trip"), each with its own currency and members.
- **Expenses** — one member pays, any subset splits the cost. Four split modes:
  - **Equal** — divide evenly across participants
  - **Exact** — each participant's amount is set explicitly; must sum to the total
  - **Percentage** — percentages must sum to 100
  - **Shares** — integer shares, cost split proportionally
- **Settlements** — record a direct payment from one member to another; moves the balances.
- **Balances** — derived per member per group. Invariant: they always sum to zero.
- **Simplified debts** — collapse a tangle of pairwise balances into the minimum set of transfers that clears them.

## Tech

- .NET 8 / C# minimal APIs
- EF Core + SQLite (file-based)
- JWT bearer auth (24h access tokens)
- FluentValidation for request shapes
- xUnit + FluentAssertions for tests, `WebApplicationFactory` for integration tests
- RFC 7807 Problem+JSON for all non-2xx responses
- `Idempotency-Key` support on `POST /expenses` and `POST /settlements`
- OpenAPI / Swagger UI at `/swagger` in development

## Project layout

```
src/
  SplitBook.Api/            ASP.NET Core minimal-API host, Features/ folders per slice
  SplitBook.Domain/         Pure-logic bits (balance calc, debt simplifier)
  SplitBook.Infrastructure/ EF Core, JWT services
tests/
  SplitBook.Api.Tests/      Integration tests
  SplitBook.Domain.Tests/   Unit tests for the pure stuff
specs/                      Product + technical spec, slice plan
```

## Running

```bash
dotnet test                          # full suite
cd src/SplitBook.Api && dotnet run   # starts on http://localhost:5124 by default
```

Swagger UI is at `http://localhost:5124/swagger` when running in Development.

## Status

Work in progress — built slice by slice against a fixed plan in `specs/slice-plan.md`. Each slice is a single vertical feature (register/login, create group, add expense, etc.) with its own integration tests. Balances-sum-to-zero and "soft-delete only" are invariants the test suite enforces on every slice.

## Out of scope

- Multi-currency FX / cross-currency expenses
- Receipt uploads, comments, reactions
- Push notifications or email
- OAuth / social login
- Real-time sync / websockets
- Multi-tenant or organization accounts

## License

MIT.
