# Slice Plan — Execution Order

Each slice is a self-contained TDD cycle. Do not advance until the current slice's Definition of Done (see technical-spec §8) is met.

## Sizing rule

**One endpoint per slice. At most two, and only if they are tightly coupled** (share infra that isn't otherwise justified, e.g. `Register` + `Login` share JWT setup; `add member` + `remove member` share membership lifecycle). Bigger slices burn the model — it spirals in deliberation, tests get written in huge batches with no feedback, and a single wrong guess contaminates many files. Small slices give per-endpoint red→green cycles and commit-worthy checkpoints.

If you feel a slice is growing to three endpoints, split it.

## Slices

| # | Slice | Endpoint(s) / Unit | DoD signal |
|---|-------|---------------------|------------|
| 0 | **Bootstrap** — solution, Api project, Tests project, `GET /health`, CI script | `GET /health` | `/health` returns 200 in an integration test; `dotnet build -warnaserror` clean |
| 1 | **Auth — Register + Login** (two coupled: share JWT infra, DbContext, password hashing) | `POST /auth/register`, `POST /auth/login` | Can register then login and receive a JWT; protected endpoint returns 401 without token |
| 1.1 | **Patch — Database initialization + smoke test** | No new endpoint. Make `dotnet run` produce a working API on a fresh file system (create `Users` table at startup), and add a smoke-test script that proves it end-to-end. | See "Slice 1.1 — special shape" below. |
| 2 | **Groups — Create** | `POST /groups` | Creator becomes a member; response includes id + currency |
| 3 | **Groups — List my groups** | `GET /groups` | Caller sees groups they belong to, not others' |
| 4 | **Groups — Detail** | `GET /groups/{id}` | Returns members; 404 when caller is not a member (not 403 — see technical-spec §5) |
| 5 | **Groups — Add + remove member** (two coupled: share membership lifecycle) | `POST /groups/{id}/members`, `DELETE /groups/{id}/members/{userId}` | Lookup by email on add; remove fails if non-zero balance exists (stub — made real in slice 11) |
| 6 | **Groups — Archive** | `POST /groups/{id}/archive` | Fails on non-zero balance |
| 7 | **Expenses — Add (Equal split only)** | `POST /groups/{groupId}/expenses` with `splitMethod: "Equal"` | €60 equal split between 2 participants stores 2 `ExpenseSplit` rows of €30, sum = total, idempotency works |
| 8 | **Expenses — Exact split** (extends slice 7, same endpoint) | `splitMethod: "Exact"` validation | Sum-of-amounts must equal total or 400 |
| 9 | **Expenses — List** | `GET /groups/{groupId}/expenses` | Paging (`skip`/`take`), date filter |
| 10 | **Expenses — Percentage + Shares** (two coupled: both are proportional) | `splitMethod: "Percentage"`, `splitMethod: "Shares"` validation | % must sum to 100; shares ≥ 1 each |
| 11 | **Expenses — Edit** (introduces `RowVersion` concurrency) | `PUT /groups/{groupId}/expenses/{id}` | Stale `If-Match` returns 412 |
| 12 | **Expenses — Delete** | `DELETE /groups/{groupId}/expenses/{id}` | Soft delete — row remains with `DeletedAt`, excluded from queries |
| 13 | **Balances** — `BalanceCalculator` + endpoint | `GET /groups/{groupId}/balances` | Invariant: sum of balances = 0 for any expense set |
| 14 | **Settlements — Record** | `POST /groups/{groupId}/settlements` | Payer→payee must both be group members; amount moves balances |
| 15 | **Settlements — List** | `GET /groups/{groupId}/settlements` | Ordered newest first |
| 16 | **Simplified debts** — `DebtSimplifier` + endpoint | `GET /groups/{groupId}/simplified-debts` | Produces ≤ N−1 transfers for N non-zero members; full product-spec §8 e2e passes |
| 17 | **User summary** | `GET /users/me/summary` | Sum across all groups; matches per-group balances |

## Slice 1.1 — special shape (patch slice, no unit tests)

Slice 1's 16 passing xUnit tests hid a production bug: `AppFactory.cs` calls `EnsureCreatedAsync()` before every test class, but `Program.cs` does not, so a fresh `dotnet run` hits `SqliteException: 'no such table: Users'` on the first request. This slice fixes that — but unit tests can't catch this category of failure (they're what caused the gap), so this slice uses a **different** definition of done.

### What to build

1. **Database schema at startup.** Either:
   - Quick path: call `db.Database.EnsureCreated()` in `Program.cs` on startup (dev-appropriate; fine for v1).
   - Proper path: add an EF migration via `dotnet ef migrations add InitialCreate` in `src/SplitBook.Api`, commit it into `Infrastructure/Persistence/Migrations/`, and call `Database.Migrate()` at startup. Matches technical-spec §6.

   Pick one. Document the choice in `DECISIONS.md` as `D-04` with the rationale. Either is acceptable. Do not do both.

2. **Smoke-test script** at `scripts/smoke.sh` that:
   - Removes any existing `src/SplitBook.Api/splitbook.db`.
   - Starts the API in the background on a known port (e.g. `127.0.0.1:5080`), capturing logs to `/tmp/api.log`.
   - Waits for `/health` to return 200 (timeout 30s).
   - `POST /auth/register` with a test user → asserts HTTP 201.
   - `POST /auth/login` with the same credentials → asserts HTTP 200, extracts the JWT.
   - `GET /groups/` without a token → asserts HTTP 401.
   - `GET /groups/` with the JWT → asserts HTTP 200.
   - Kills the API, cleans up the DB file.
   - Exits 0 on all-pass, non-zero with a clear message on any failure.

### Protocol differences vs normal slices

- **Skip `@test-writer`.** No xUnit tests this slice. The smoke script is the test.
- Primary writes `scripts/smoke.sh` + the startup fix.
- `@reviewer` verifies: (a) smoke script is present and executable, (b) `bash scripts/smoke.sh` exits 0 from a clean state, (c) the startup change is minimal and does not touch code outside `Program.cs` + optionally `Infrastructure/Persistence/Migrations/`.
- `@lessons-scribe` should probably produce one lesson from this slice: "tests pass, prod broken" is now a known category we need to guard against in future slices.

### DoD

`bash scripts/smoke.sh` exits 0 against a fresh filesystem. The 16 existing xUnit tests still pass. The diff touches only: `Program.cs`, `scripts/smoke.sh` (new), `DECISIONS.md`, and — if the proper path was chosen — files under `Infrastructure/Persistence/Migrations/` and a one-line package reference. Anything else is out of scope.

## Notes for the implementer

- **Do not skip ahead.** Do not add a `Settlements` table in slice 2 because you "know it's coming." Each slice introduces only what its tests require.
- **Red first.** Start each slice by writing integration tests that fail because the endpoint 404s. The test-writer subagent is responsible for this. Confirm red by quoting `dotnet test` output.
- **One open question per slice, max.** If a slice surfaces more than one architectural decision, stop and escalate to the reviewer subagent rather than guessing.
- **Refactor is part of the slice.** The slice is not done until the code you just wrote is clean — dead code, placeholder names, duplicated mapping, method bodies that never execute, etc., all go before DoD.
- **Smoke gate.** After xUnit is green, invoke `@smoke-tester` to extend `scripts/smoke.sh` and verify the real API over HTTP. A slice is not done until smoke is green. Applies to every slice except 0.
- **The definition of "done" is strict.** If you find yourself thinking "we'll clean that up in a later slice," either make that an explicit follow-up noted in the slice log OR clean it up now. Both are acceptable; silent deferral is not.
