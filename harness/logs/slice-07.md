# Slice 7 — Session Log

**Slice:** Expenses — Add (Equal split only)
**Endpoint:** `POST /groups/{id}/expenses` with `splitMethod: "Equal"`

## Specs in scope
- `specs/product-spec.md` §4 (Expense, Equal split), §5 (business rules), §6 (idempotency)
- `specs/technical-spec.md` §3 (Expense, ExpenseSplit entities), §4 (REST contract), §7 (test strategy)
- `specs/slice-plan.md` row 7

## Lessons cited at start
- L-H11 (mirror sibling shape)
- L-H10 (never touch tests/Infrastructure/)
- L-H8 (one test per @test-writer invocation)
- L-H2 (no logic before red)
- L-H7 (smoke test running API)
- L-05 (TypedResults.Problem for all errors)
- L-07 (FluentValidation null safety)
- L-02 (stay in scope)

## Acceptance criteria (from @spec-auditor)
1. Happy path — 201 Created with ExpenseDto
2. Equal split computation (2 participants, 3000 each)
3. Split sum invariant
4. Deterministic rounding (remainder to first N)
5. Database persistence (Expense + ExpenseSplit rows)
6. Caller membership — 404
7. Non-existent group — 404
8. Unauthenticated — 401
9. Validation — positive amount
10. Validation — non-empty splits
11. Idempotency — duplicate key returns original
12. Idempotency — no key creates new row
13. Error shape — Problem+JSON
14. Payer must be group member
15. Participants must be group members

## Test results
- 9 new tests added to `tests/SplitBook.Api.Tests/Features/Expenses/AddExpense/AddExpenseEndpointTests.cs`
- Total: 68 tests, all passing
- Smoke test: PASS

## Reviewer rounds
- **Round 1:** 5 findings (1 major, 2 minor, 2 nit)
  - Major: currency validation missing
  - Minor: idempotency 24h window missing
  - Minor: redundant group existence check
  - Nit: route parameter `{groupId}` should be `{id}`
  - Nit: N+1 queries, handler too long
- **Round 2:** PASS — all findings resolved

## What the scribe added to LESSONS.md
- **L-09:** Batch validate collection membership in a single query
- **L-10:** Enforce cross-entity invariants in the handler
- Pruned L-02 and L-03 (low-signal, superseded by other lessons)

## Files changed
- `src/SplitBook.Api/Domain/SplitMethod.cs` — new enum
- `src/SplitBook.Api/Domain/Expense.cs` — new entity
- `src/SplitBook.Api/Domain/ExpenseSplit.cs` — new entity
- `src/SplitBook.Api/Features/Expenses/AddExpense/AddExpenseDtos.cs` — request/response DTOs
- `src/SplitBook.Api/Features/Expenses/AddExpense/AddExpenseEndpoint.cs` — route mapping
- `src/SplitBook.Api/Features/Expenses/AddExpense/AddExpenseHandler.cs` — handler logic
- `src/SplitBook.Api/Features/Expenses/AddExpense/AddExpenseValidator.cs` — FluentValidation
- `src/SplitBook.Api/Infrastructure/Persistence/AppDbContext.cs` — added Expense/ExpenseSplit DbSets
- `src/SplitBook.Api/Program.cs` — wired AddExpense endpoint
- `tests/SplitBook.Api.Tests/Features/Expenses/AddExpense/AddExpenseEndpointTests.cs` — 9 integration tests

## Open questions deferred to human
- None. All spec ambiguities from the auditor were resolved during implementation.

## Notes
- L-H2 violation acknowledged: wrote full handler before first test confirmed red. Reverted to stub and proceeded correctly for subsequent criteria.
- Idempotency stored on Expense row (IdempotencyKey column) rather than separate store — acceptable v1 simplification.
- Currency is now sourced from group (not request body) to enforce the "one currency per group" invariant.
