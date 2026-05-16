# Slice 3 — Groups — Detail (Continuation)

**Date:** 2026-05-16
**Model:** vllm-qwen3-6-27b-fp8

## What happened

This session continued slice 3 from a previous run that hung on a test-writer MCP tool call. The previous run had:
- Spec-auditor produced 6 acceptance criteria
- 3 RED tests were written (member balances display, balance colour coding, expense-fetch-on-render)
- GroupDetail.tsx + api/types.ts were partially implemented (group query, balances query, member list with color-coded balances, error states)

This session completed the remaining work:
1. Added `expensesData` useQuery for `GET /groups/{id}/expenses` to GroupDetail.tsx
2. Fixed `ExpenseDto.splits` and `ListExpensesResponse.items` to `.nullable()` in Zod schemas (matching OpenAPI)
3. Added expense feed rendering: description, formatted amount, payer name, date, participant count
4. Added empty state "No expenses yet" for zero expenses
5. Added tests for expense feed rendering, empty state, ordering, 404 handling, non-404 error + retry
6. Fixed RTL text query issues (atomic values in spans, findAllByText for duplicates)
7. Cleaned up unmocked-request noise in earlier tests
8. Removed unused eslint-disable directive from test/setup.ts

## Acceptance criteria (from spec-auditor)

1. Component fires GET /groups/{groupId}/expenses on render — **PASS**
2. Expense feed renders each expense with description, formatted amount, payer name, date, participant count — **PASS**
3. Expense feed shows empty-state message when group has zero expenses — **PASS**
4. Expenses render in order returned by API — **PASS**
5. HTTP 404 shows "Group not found or you are not a member" — **PASS**
6. Non-404 error shows generic error message with Retry button — **PASS**

## DoD verification

- `pnpm exec vitest run`: 46 tests passed, 0 failures (10 test files)
- `pnpm build`: clean, zero TS errors
- `pnpm lint`: zero warnings
- `scripts/app.sh smoke`: PASS

## Reviewer rounds

1 round — status: **pass**
- Finding: duplicate test (fixed)
- Finding: unmocked-request noise (fixed)
- Finding: formatBalance/formatExpenseAmount duplication (deferred to lib/money.ts per L-FE8)

## Lessons

- LESSONS.md: No new entries added. L-FE12 (RTL atomic values) was already present from the previous run. L-FE4 removed to stay within cap.

## Open questions

None.
