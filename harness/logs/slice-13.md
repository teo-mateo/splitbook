# Slice 13 — Balances: Session Log

## Specs in scope
- `specs/product-spec.md` §4 (Balance), §5 (balances sum to zero invariant)
- `specs/technical-spec.md` §4 (`GET /groups/{groupId}/balances`), §7 (invariants)
- `specs/slice-plan.md` row 13

## Lessons cited at start
- **L-00**: Read spec end-to-end before writing tests.
- **L-H2**: No production logic before red (N/A — implementation existed).
- **L-H8**: One test per `@test-writer` invocation.
- **L-H11**: Mirror sibling feature shape.
- **L-H10**: Test-writer never touches `Infrastructure/`.
- **L-05**: `TypedResults.Problem()` for all non-2xx.
- **L-H7**: Smoke-test the running API.
- **L-14**: SQLite cannot ORDER BY DateOnly/DateTimeOffset.
- **L-17**: Extract calculation logic before needed twice.

## What happened

The Balances feature (`GET /groups/{groupId}/balances` + `BalanceCalculator`) was already fully implemented from a previous run. This session was a verification pass:

1. Read all specs and LESSONS.md.
2. Invoked `@spec-auditor` — returned 14 acceptance criteria.
3. Audited existing code: endpoint, handler, DTOs, `BalanceCalculator`, and 12 integration + 2 unit tests.
4. Ran `dotnet test` — 132/132 passed.
5. Ran `scripts/app.sh smoke` — passed.
6. Invoked `@reviewer` — status: **pass**, two minor findings.
7. Addressed both findings:
   - Added `BalanceCalculator_Settlement_AdjustsBalancesCorrectly` unit test (covers the settlement code path in `BalanceCalculator.Calculate`).
   - Added `HasQueryFilter(s => s.DeletedAt == null)` for `Settlement` in `AppDbContext.OnModelCreating`.
8. Ran `dotnet test` — 133/133 passed.
9. Ran `scripts/app.sh smoke` — passed.
10. Invoked `@lessons-scribe` — no new lessons.

## Reviewer round count and findings
- **Round 1**: pass, 2 minor findings (both addressed).
- **Total rounds**: 1.

## Scribe output
No new lessons added to LESSONS.md.

## Open questions deferred to the human
None.
