# Slice 8 — Session Log

**Slice:** Expenses — Exact split
**Endpoint:** `POST /groups/{id}/expenses` with `splitMethod: "Exact"` (extends slice 7)

## Specs in scope
- `specs/product-spec.md` §4 (Exact split: participants each have a specified exact amount; must sum to expense total)
- `specs/technical-spec.md` §3 (ExpenseSplit entity), §4 (REST contract), §7 (test strategy)
- `specs/slice-plan.md` row 8

## Lessons cited at start
- L-H11 (mirror sibling shape)
- L-H10 (never touch tests/Infrastructure/)
- L-H8 (one test per @test-writer invocation)
- L-H2 (no logic before red)
- L-05 (TypedResults.Problem for all errors)
- L-07 (FluentValidation null safety)
- L-09 (batch membership validation)
- L-10 (cross-entity invariants)

## Acceptance criteria (from @spec-auditor)
1. Happy path — 201 Created with Exact split
2. Sum-of-amounts mismatch — 400
3. Null amountMinor — 400
4. Database persistence (exact amounts, no rounding)
5. Response shape (splitMethod: "Exact", splits array)
6. Single participant — valid (201)

## Test results
- 6 new tests added to `tests/SplitBook.Api.Tests/Features/Expenses/AddExpense/AddExpenseEndpointTests.cs`
- Total: 74 tests, all passing
- Smoke test: PASS

## Reviewer rounds
- **Round 1:** 3 findings (1 major, 2 minor)
  - Major: payer membership check broken for Exact split when payer not in splits list
  - Minor: zero-amount participants allowed (intentional)
  - Minor: `throw new InvalidOperationException` in switch (acceptable — unreachable)
- **Round 2:** PASS — all findings resolved

## What the scribe added to LESSONS.md
- **L-11:** Audit shared logic when extending existing handlers
- Pruned L-04 (low-signal, superseded by Definition of Done)

## Files changed
- `src/SplitBook.Api/Features/Expenses/AddExpense/AddExpenseHandler.cs` — extended to handle Exact split
- `tests/SplitBook.Api.Tests/Features/Expenses/AddExpense/AddExpenseEndpointTests.cs` — 6 new tests

## Open questions deferred to human
- None.

## Notes
- Ambiguity resolved: for Exact split, the payer is NOT auto-included in the splits list. The user must explicitly specify every participant and their exact amount.
- Ambiguity resolved: zero-amount participants are allowed (someone may genuinely owe nothing).
- L-H2 protocol issue: the handler was written before test-writer confirmed red for criteria 2–6. The tests are valid, but the TDD red→green signal was lost for those criteria.
