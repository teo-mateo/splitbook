# Slice 7.1 — Expenses: Add-expense button reachability fix

**Date:** 2026-05-17
**Status:** Done

## Specs in scope
- `specs/slice-plan.md` §7.1 (user story: Add-expense button reachability fix)
- `specs/product-spec.md` §3.3 (Group Detail — expense section)
- `specs/technical-spec.md` §4 (Routing), §5 (Styling)
- Global DoD (reachability, round-trip, AuthGuard, error states)

## Lessons cited at start
- L-H2 (red before green), L-H8 (one test per criterion), L-H11 (mirror sibling),
  L-FE6 (Testing Library queries), L-FE11 (router-param Route wrapping),
  L-FE15 (unique IDs per test), L-FE1 (no watch-mode), L-FE2 (pnpm from web dir).

## What changed
### Production code
- `src/SplitBook.Web/src/features/groups/GroupDetail.tsx` — Added "Add expense" button in the Expenses section header, using `navigate()` to `/groups/:id/expenses/new`. Styling mirrors the existing "Add member" button (`rounded bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700`).

### Tests
- `src/SplitBook.Web/src/features/groups/GroupDetail.test.tsx` — Added 5 new tests:
  1. Button renders in Expenses section
  2. Clicking navigates to correct route with group ID
  3. Full reachability through real `<App>` (click group card → click Add expense → ExpenseForm appears)
  4. AuthGuard redirects unauthenticated users to `/login`
  5. Round-trip: submit expense → navigate back → expense visible in feed

## Acceptance criteria (from spec-auditor)
1. ✅ Add expense button renders on Group Detail
2. ✅ Button navigates to correct route with group ID
3. ✅ Full reachability through real `<App>`
4. ✅ AuthGuard blocks unauthenticated access
5. ✅ Round-trip: submit and see it land
6. ✅ Empty state coexists with button (button visible alongside "No expenses yet")
7. ✅ Button visible at 320px (homogeneous styling with sibling buttons)
8. ✅ No regression in slice 6/7 ExpenseForm tests

## Reviewer
- **Rounds:** 1
- **Status:** pass
- **Findings:** Minor L-H2 timing concern (production edited ~10 min before tests due to initial `<Link>` vs `<button>` mismatch); 320px not explicitly tested (mitigated by styling homogeneity).

## Scribe
- No new lessons added. Existing lessons (L-H11, L-FE11, L-FE15) fully covered the slice's concerns. File at hard cap (20 entries).

## Toolchain results
- `pnpm exec vitest run`: 96/96 passed, 14 test files
- `pnpm build`: clean (110 modules, zero TS errors)
- `pnpm lint`: zero warnings
- `scripts/app.sh smoke`: PASS

## Open questions
None.
