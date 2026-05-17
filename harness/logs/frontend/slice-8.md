# Slice 8 — Expenses: List + Pagination

## Date
2026-05-17

## Specs in scope
- `specs/slice-plan.md` — user story §8 (Expenses: List + Pagination)
- `specs/product-spec.md` §3.3 (Group Detail expense feed), §5 (error handling)
- `specs/technical-spec.md` §3.3 (TanStack Query cache invalidation)
- `specs/openapi.json` — `GET /groups/{groupId}/expenses` with `skip`/`take`/`from`/`to` params

## Lessons cited at start
- L-H2 (Red before green), L-H8 (One test per criterion), L-H11 (Mirror nearest sibling)
- L-FE1 (No watch mode), L-FE2 (Work from src/SplitBook.Web), L-FE6 (Testing Library queries)
- L-FE11 (Router-param tests need real Route), L-FE15 (Query cache isolation), L-FE14 (Verify test-writer output)

## Acceptance criteria (from @spec-auditor)
16 criteria covering: pagination query params, expense row fields, newest-first ordering, next-page controls, conditional page controls, date filter from/to params, clearing filter, round-trip, loading/empty/error states, money formatting, AuthGuard, 401 handling, 320px viewport, reachability through real `<App>`.

## What changed
- **`src/features/expenses/ExpenseList.tsx`** — Implemented from empty stub. TanStack Query with `skip`/`take`/`from`/`to` params, pagination (Previous/Next), date filter with Clear button, loading/empty/error states. Page size: 10.
- **`src/features/expenses/ExpenseItem.tsx`** — Implemented from empty stub. Renders payer name, formatted amount (via `lib/money.ts` `formatCurrency`), description, date, participant count.
- **`src/features/groups/GroupDetail.tsx`** — Replaced inline expense rendering with `<ExpenseList>` component. Removed inline `expensesData` query, `memberNameMap`, `formatExpenseAmount`.
- **`src/features/expenses/ExpenseList.test.tsx`** — New test file with 9 tests: pagination params, expense row fields, empty state, loading state, next-page navigation, date filter from/to, clearing filter, error state with retry, reachability through real `<App>`.

## Reviewer round count and findings
1 round. Status: **pass**. Minor findings addressed:
- Used `DateInput` shared component instead of raw `<input type="date">` (L-H11)
- Cleaned up redundant dynamic `userEvent` imports
- Removed blank lines left in `GroupDetail.tsx`

## Scribe additions to LESSONS.md
- **L-FE18** (updated): Adding query-firing children to shared parents pollutes sibling test files — must check stderr after full suite run.

## Open questions deferred to human
- None. The stderr noise in `ExpenseForm.test.tsx` is acknowledged but deferred — those tests pass and the fix belongs in the ExpenseForm test file, not this slice.

## DoD checklist
- [x] Full `pnpm exec vitest run` passes — 105/105 tests, 0 regressions
- [x] `pnpm build` clean — 113 modules, 0 TS errors
- [x] `pnpm lint` clean — 0 warnings
- [x] `scripts/app.sh smoke` passes — build + dev server + `/` contains "SplitBook"
- [x] @reviewer status: pass
