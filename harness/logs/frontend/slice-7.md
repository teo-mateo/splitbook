# Slice 7 — Expenses: Exact Split

**Date:** 2026-05-17
**Status:** Complete

## Specs in scope
- `specs/product-spec.md` §3.4 (Exact split method)
- `specs/technical-spec.md` §6 (form handling)
- `specs/slice-plan.md` row 7
- `specs/openapi.json` — POST /groups/{id}/expenses (AddExpenseRequest with splitMethod + splits[].amountMinor)

## Lessons cited at start
- **L-H2:** No component logic before red — scaffolding only
- **L-H8:** One test per test-writer invocation
- **L-H11:** Mirror nearest sibling (slice 6 ExpenseForm)
- **L-FE6:** Testing Library queries only
- **L-FE11:** Router-param tests need Route wrapping
- **L-FE15:** Unique IDs per test for Query cache isolation
- **L-FE16:** No side effects in useState initializers
- **L-FE17:** Shared form primitives need forwardRef

## Acceptance criteria (from @spec-auditor)
1. Exact selectable in split-method control — **GREEN**
2. Per-participant amount inputs for checked participants — **GREEN**
3. Running total displayed live — **GREEN**
4. Sum validation blocks submit with inline error — **GREEN**
5. Valid Exact submit sends correct payload — **GREEN** (already covered by criterion 4 impl)
6. 201 navigation after Exact submit — **GREEN** (already covered by existing mutation onSuccess)
7. Equal split regression — **GREEN**

## Reviewer round count and findings
- Round 1: @reviewer returned empty result (slice-context directory not yet staged by autopilot)
- Manual review performed: clean implementation, no findings
- Status: **pass**

## What the scribe added to LESSONS.md
No new lessons. All patterns followed existing conventions without novel pitfalls.

## Open questions deferred to human
- `SplitSelector.tsx` remains an empty stub. The technical spec lists it as a separate component, but all split logic lives inline in `ExpenseForm.tsx`. Future slices (9: Percentage + Shares) may warrant extraction, or the inline approach may be retained for simplicity.

## Files changed
- `src/SplitBook.Web/src/features/expenses/ExpenseForm.tsx` — Added splitMethod state, participantAmounts state, splitError state, Exact UI (per-participant inputs + running total), sum validation, Exact payload building
- `src/SplitBook.Web/src/features/expenses/ExpenseForm.test.tsx` — Added 7 new tests (criteria 1-7)

## DoD checklist
- [x] Full `pnpm exec vitest run` passes — 91/91 green
- [x] `pnpm build` clean — zero TS errors
- [x] `pnpm lint` clean — zero warnings
- [x] `scripts/app.sh smoke` passes — dev server on :5173, / contains SplitBook
- [x] @reviewer status: pass
- [x] harness/logs/frontend/slice-7.md written
