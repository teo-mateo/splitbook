# Slice 11 — Expenses — Edit | Session Log

## Specs in scope
- `specs/product-spec.md` §5 (business rules: append-only, soft edit)
- `specs/technical-spec.md` §4 (REST contract: `PUT /groups/{groupId}/expenses/{id}`), §6 (RowVersion)
- `specs/slice-plan.md` row 11

## Lessons cited at start
- L-H2 (no logic before red)
- L-H11 (mirror nearest sibling)
- L-H8 (one test per @test-writer invocation)
- L-H10 (never touch Infrastructure/)
- L-05 (TypedResults.Problem for all errors)
- L-11 (audit shared logic when extending handlers)
- L-H7 (smoke-test the running API)
- L-12 (verbose on 5xx)
- L-15 (test data rejects wrong implementations)

## What happened

The `EditExpense` handler, endpoint, and 11 tests were already fully implemented from a prior incomplete session that never wrote the slice log. The handler covers:
- Happy-path edit with description update and Version increment
- Stale `If-Match` → 412
- Missing `If-Match` → 412
- Non-existent expense → 404
- Caller not a group member → 404 (not 403)
- Unauthenticated → 401
- Invalid body (AmountMinor = 0) → 400
- Currency mismatch → 400
- Payer not a group member → 400
- Participant not a group member → 400
- Split method change (Equal → Exact) with split rebuild

No code changes were needed. Verified:
- `dotnet test` — 107/107 passed, 0 failed, 0 skipped
- `scripts/app.sh smoke` — /health 200, /swagger 301, /swagger/v1/swagger.json 200

## Reviewer round count and findings
- **Round 1:** Status `pass`. Two minor findings:
  1. ~100 lines of split calculation code duplicated between `AddExpenseHandler` and `EditExpenseHandler` (noted as future improvement, not a blocker).
  2. Cross-feature `using` from EditExpense to AddExpense for shared DTOs/validator (acceptable per spec — edit uses "same body" as add).
- No fix rounds required.

## Scribe output
Added L-17 to LESSONS.md: "Extract calculation logic before it's needed twice" — when an edit handler mirrors create logic, extract into a shared helper rather than duplicating.

## Open questions deferred to the human
- The ~100-line duplication between AddExpense and EditExpense split calculators should be addressed in a future refactor (possibly slice 12 or a dedicated cleanup slice). The reviewer flagged it as minor; the human decides priority.
