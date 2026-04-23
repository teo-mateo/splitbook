# DECISIONS.md

Architectural decisions that are fixed inputs to every slice. The primary must honor these without re-litigating them; the reviewer must treat violations as `major` findings. New entries are added by the human, or by the primary when it makes a choice the spec left open.

---

## D-01: Handler pattern — plain minimal APIs

Use ASP.NET Core minimal APIs. No MediatR. No FastEndpoints. No Carter.

Per feature folder under `src/SplitBook.Api/Features/`:

- one static class `<Feature>Endpoint` with a `Map<Feature>(RouteGroupBuilder group)` extension method that wires the route to a handler method,
- one `<Feature>Handler` class containing the actual logic, constructor-injected via DI,
- request/response DTOs as separate files,
- a `<Feature>Validator` (FluentValidation) when the request has non-trivial rules.

Endpoint mapping stays a one-liner: `group.MapPost("/register", RegisterHandler.HandleAsync)`. All logic lives in the handler — endpoints do not orchestrate.

**Rationale:** smallest possible API surface for the model to hallucinate against; no framework magic for the reviewer to miss; TDD signal stays pure (a failing test fails against the handler class, not against framework plumbing).

## D-02: Project structure — single project

Single `SplitBook.Api` project with `Features/`, `Domain/`, and `Infrastructure/` folders as described in technical-spec §2. No separate class libraries.

Reconsider splitting `Domain/` into its own class library at **slice 6** (Balances), when pure-domain unit tests for `BalanceCalculator` land and physical separation would prevent accidental framework dependencies in domain code.

Until that slice lands, `SplitBook.Domain.Tests/` stays a folder inside `tests/SplitBook.Api.Tests/` — not a separate project.

**Rationale:** fewer moving parts, fewer `.csproj` references for the model to mis-wire, less solution-file churn per slice.

## D-04: Database initialization — EnsureCreated (quick path)

Use `db.Database.EnsureCreated()` in `Program.cs` after `builder.Build()`, wrapped in a scoped service resolution. No EF migrations for v1.

**Rationale:** Slice 1.1's goal is minimal repair of the "tests green, prod broken" startup bug. `EnsureCreated()` is one line, requires no EF CLI tooling, no migration files, and no `.csproj` changes. The technical-spec §6 explicitly allows simplifications for v1. If schema evolution requires versioned migrations later, we can migrate to `Database.Migrate()` then and document the follow-up decision.
