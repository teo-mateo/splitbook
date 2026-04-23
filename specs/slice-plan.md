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
| 2 | **Groups — Create** | `POST /groups` | Creator becomes a member; response includes id + currency |
| 3 | **Groups — List my groups** | `GET /groups` | Caller sees groups they belong to, not others' |
| 4 | **Groups — Detail** | `GET /groups/{id}` | Returns members; 404 when caller is not a member (not 403 — see technical-spec §5) |
| 4.1 | **Patch — Share DTOs between API and tests** | No new endpoint, no new tests. Make all API request/response DTOs `public`, have the tests project reference them via the existing project reference, and remove every DTO record currently duplicated inside test files. | See "Slice 4.1 — special shape" below. |
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

## Slice 4.1 — special shape (refactor slice, no new tests)

Through slices 1–4, tests have either parsed responses via `JsonDocument.Parse` or declared test-local DTO records (e.g. `internal record GroupDetailDto(...)` at the bottom of a test class). Both approaches are wrong long-term:

- The test-local DTOs duplicate definitions already present in the API project, so a rename on either side silently drifts.
- The JSON parsing approach doesn't exercise the real wire contract — tests can pass while the actual shape diverges.

The fix: make every API request/response DTO `public`, reference them from the tests project, and use them directly. This is a housekeeping slice with no new behavior and no new tests.

### What to change

1. **Make API DTOs public.** Every request/response record under `src/SplitBook.Api/Features/**/` (e.g. `RegisterRequest`, `RegisterResponse`, `LoginRequest`, `LoginResponse`, `CreateGroupRequest`, `GroupResponse`, `GroupDetailResponse`, `MemberResponse`, etc.) must be `public`. If any currently have no visibility modifier, C# defaults them to `internal`; add `public` explicitly.

2. **Verify project reference.** `tests/SplitBook.Api.Tests/SplitBook.Api.Tests.csproj` must have `<ProjectReference Include="../../src/SplitBook.Api/SplitBook.Api.csproj" />`. It almost certainly already does (the test fixture uses `WebApplicationFactory<Program>`). Confirm.

3. **Remove all test-local DTO records.** Search for `internal record .*Dto` and `record .*Dto` inside `tests/`. Every such declaration that duplicates an API DTO must be deleted, replaced by a `using` of the API's namespace (e.g. `using SplitBook.Api.Features.Auth.Register;`) and references to the real type.

   Allowed exceptions:
   - DTOs that genuinely exist only for testing (e.g. a `ProblemDetailsDto` for parsing RFC 7807 responses if the API doesn't define one) — keep in a single file `tests/SplitBook.Api.Tests/Infrastructure/TestOnlyDtos.cs`, not scattered per-feature.
   - Deserialization shape for external/third-party response envelopes if any appear later.

4. **`JsonDocument.Parse` audit.** Remove any `JsonDocument.Parse` calls that now have a typed equivalent. Keep it only where it's genuinely right: single-field lookups (extracting `accessToken` from a login response is fine; JWT payload claim inspection is fine).

### Protocol differences vs normal slices

- **Skip `@test-writer`** — no new tests.
- Primary does the refactor directly.
- Primary must run the full test suite and confirm **all pre-existing tests still pass** at every step (not just at the end).
- `@reviewer` verifies: (a) no `record .*Dto` declarations remain inside test files that duplicate API types, (b) the test suite is green, (c) the diff does not introduce any behavioral changes (no new handler logic, no spec-level changes).
- `@lessons-scribe` may have nothing to add unless a generalizable DTO-sharing principle emerges.

### DoD

- `dotnet test` green — all existing tests pass unchanged.
- `grep -rn 'record [A-Z][a-zA-Z]*Dto' tests/` returns empty (or only references inside `tests/SplitBook.Api.Tests/Infrastructure/TestOnlyDtos.cs`).
- All API request/response types referenced from tests are visible as `public` from the Api assembly.
- Diff touches test files, Api DTO visibility modifiers, and possibly one `using` in each test file — nothing else.

## Notes for the implementer

- **Do not skip ahead.** Do not add a `Settlements` table in slice 2 because you "know it's coming." Each slice introduces only what its tests require.
- **Red first.** Start each slice by writing integration tests that fail because the endpoint 404s. The test-writer subagent is responsible for this. Confirm red by quoting `dotnet test` output.
- **One open question per slice, max.** If a slice surfaces more than one architectural decision, stop and escalate to the reviewer subagent rather than guessing.
- **Refactor is part of the slice.** The slice is not done until the code you just wrote is clean — dead code, placeholder names, duplicated mapping, method bodies that never execute, etc., all go before DoD.
- **The definition of "done" is strict.** If you find yourself thinking "we'll clean that up in a later slice," either make that an explicit follow-up noted in the slice log OR clean it up now. Both are acceptable; silent deferral is not.
