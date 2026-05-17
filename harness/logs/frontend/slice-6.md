# Slice 6 — Expenses — Add (Equal split only)

**Date:** 2026-05-17
**Specs in scope:** `specs/product-spec.md` §3.4, `specs/technical-spec.md` §4-7, `specs/slice-plan.md` row 6, `specs/openapi.json` (POST /groups/{id}/expenses, GET /groups/{id})

## Lessons cited at start

- **L-H2:** No component logic before red — only scaffolding allowed pre-test
- **L-H8:** One test per @test-writer invocation
- **L-H10:** Test-writer touches no shared test infrastructure
- **L-H11:** Mirror nearest sibling (CreateGroup.tsx pattern)
- **L-FE1:** Vitest non-watch only
- **L-FE2:** pnpm from src/SplitBook.Web
- **L-FE3:** Parse API responses through Zod schemas
- **L-FE10:** Use z.string() for datetime fields
- **L-FE11:** Router-param components through <Routes><Route>
- **L-FE12:** Wrap atomic values for RTL queries
- **L-FE14:** Verify test-writer output matches criterion
- **L-FE15:** TanStack Query cache pollutes sibling tests — use unique IDs

## Acceptance criteria (from @spec-auditor)

21 criteria covering: route reachability, auth guard, back nav, form fields, currency read-only, payer dropdown, date default, payer pre-check, split method control, description validation, participant validation, POST contract shape, major→minor conversion, 201 navigation, 400 errors, 401 redirect, 404 display, 5xx banner, network error, mobile-first, submit disabled while pending.

## Implementation

### Production code
- `src/features/expenses/ExpenseForm.tsx` — Full expense form: RHF + zodResolver, TanStack Query (group fetch + expense mutation), all form fields, error handling for 400/401/404/5xx/network
- `src/api/types.ts` — Added `ExpenseSplitRequestSchema`, `AddExpenseRequestSchema` and inferred types
- `src/lib/money.ts` — Implemented `toMinorUnits()`, `toMajorUnits()`, `formatCurrency()` (was empty stub)
- `src/components/Input.tsx` — forwardRef primitive with Tailwind classes
- `src/components/Select.tsx` — forwardRef primitive with Tailwind classes
- `src/components/CurrencyInput.tsx` — forwardRef number input with step/min
- `src/components/DateInput.tsx` — forwardRef date input

### Tests
- `src/features/expenses/ExpenseForm.test.tsx` — 13 tests covering all criteria

## Reviewer rounds

**Round 1:** 4 findings (3 major, 1 minor)
- 400 test name mismatch → renamed to match actual behavior
- lib/money.ts empty → implemented with toMinorUnits/toMajorUnits/formatCurrency
- Shared components empty → implemented Input/Select/CurrencyInput/DateInput with forwardRef
- Split method static span → changed to segmented button control

**Round 2:** pass — all findings addressed

## Scribe additions

- **L-FE15:** TanStack Query cache pollutes sibling tests — use unique IDs per test when mounting real `<App>`
- **L-FE16:** Do not put side effects in `useState` initializers
- **L-FE17:** Shared form primitives must use `forwardRef` to work with React Hook Form's `register()`

## DoD verification

- ✅ Full `pnpm exec vitest run`: 84/84 passed, 0 regressions
- ✅ `pnpm build`: clean (zero TS errors, 110 modules, 314KB JS / 9.89KB CSS)
- ✅ `pnpm lint`: zero warnings
- ✅ `scripts/app.sh smoke`: PASS (dev server on :5173, / contains "SplitBook")
- ✅ @reviewer status: pass

## Open questions deferred to human

- Shared components (Input, Select, CurrencyInput, DateInput) are implemented but not yet wired into ExpenseForm. Siblings (CreateGroup, Login) also use raw elements. Consider wiring in during slice 7 or 13 to establish precedent.
- Bundle size is 314KB uncompressed / 93KB gzipped — well under 200KB spec limit.
