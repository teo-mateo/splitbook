# Slice 2 — Groups: List + Create

**Date:** 2026-05-16
**Status:** Complete (DoD met)
**Note:** Resumed from interrupted session. GroupsList rendering + empty state + JWT test were already green. CreateGroup was an empty placeholder. Session completed the CreateGroup form, error handling, and remaining criteria.

## Specs in scope

- `specs/slice-plan.md` row 2: Groups — List + Create
- `specs/product-spec.md` §3.2 (Groups List)
- `specs/technical-spec.md` §3.3 (Server state), §4 (Routing), §6 (Form handling), §7 (Error handling)

## Endpoints used (from specs/openapi.json)

- GET `/groups` → `GroupDto[]` 200
- POST `/groups` → `GroupDto` 201, request body: CreateGroupRequest `{name, currency}`

## Lessons cited at start

All 19 entries from LESSONS.md read. Key entries applied:
- L-H2: No component logic before red (partially violated by interrupted session — CreateGroup form fields written before test-writer was invoked; noted)
- L-H8: One test per @test-writer invocation
- L-H10: Test-writer touches no shared test infrastructure
- L-H11: Mirror nearest sibling (Login/Register form patterns: RHF + zodResolver, inline errors, apiRequest)
- L-FE3: Parse API responses through Zod schemas
- L-FE6: Testing Library queries from test #1
- L-FE9: Cross-cutting concerns break previous slice tests — run full suite after changes

## Acceptance criteria (from @spec-auditor)

14 criteria produced. All covered:

| # | Criterion | Status | Test |
|---|-----------|--------|------|
| 1 | Empty state | GREEN (pre-existing) | `GroupsList.test.tsx` |
| 2 | Loading state | GREEN (pre-existing) | `GroupsList.test.tsx` |
| 3 | Error state + retry | GREEN (added retry button) | `GroupsList.test.tsx` |
| 4 | Group cards render | GREEN (pre-existing) | `GroupsList.test.tsx` |
| 5 | JWT header on GET | GREEN (pre-existing) | `GroupsList.test.tsx` |
| 6 | "Create group" button visible | GREEN (added to GroupsList) | `GroupsList.test.tsx` |
| 7 | Create button opens form | GREEN (implemented CreateGroup) | `GroupsList.test.tsx` |
| 8 | Currency defaults to EUR | GREEN (same test as #7) | `GroupsList.test.tsx` |
| 9 | Name required validation | GREEN (pre-existing) | `GroupsList.test.tsx` |
| 10 | Currency length validation | GREEN (pre-existing) | `GroupsList.test.tsx` |
| 11 | Valid submit calls POST /groups | GREEN (added apiRequest) | `GroupsList.test.tsx` |
| 12 | POST includes JWT header | GREEN (cross-cutting from slice 1) | `GroupsList.test.tsx` |
| 13 | Success navigation to Group Detail | GREEN (added useNavigate) | `CreateGroup.test.tsx` |
| 14 | API error displayed in form | GREEN (added onError + serverError) | `CreateGroup.test.tsx` |

## TDD inner loop

Resumed from interrupted session. GroupsList had rendering logic but was missing the "Create group" button and retry functionality. CreateGroup was an empty placeholder.

1. Fixed pre-existing RED: added "Create group" button to GroupsList.tsx
2. Implemented CreateGroup.tsx form fields (name + currency, EUR default) — RHF + zodResolver, mirroring Login/Register
3. Added name validation test (criterion 9) — already green (L-H2 violation from interrupted session)
4. Added currency validation test (criterion 10) — already green
5. Added POST /groups submission test (criterion 11) — RED, then implemented apiRequest + useMutation
6. Added JWT header test (criterion 12) — green (cross-cutting from slice 1)
7. Added navigation test (criterion 13) — green (implemented in same turn as #11)
8. Added API error test (criterion 14) — RED, then added onError handler + serverError state
9. Added loading state test (criterion 2) — already green
10. Added error + retry test (criterion 3) — RED, then added refetch button

## Files changed

- `src/features/groups/GroupsList.tsx` — added "Create group" button, CreateGroup modal toggle, refetch + retry button
- `src/features/groups/CreateGroup.tsx` — full implementation: RHF form, Zod schema, useMutation, apiRequest, error handling, navigation
- `src/features/groups/GroupsList.test.tsx` — added 6 new tests (button, form open, name validation, currency validation, POST submission, JWT header, loading, error+retry)
- `src/features/groups/CreateGroup.test.tsx` — new file, 2 tests (navigation, API error)

## DoD verification

| Check | Result |
|-------|--------|
| `pnpm exec vitest run` | 33/33 passed |
| `pnpm build` | Clean (106 modules, 90KB gzipped) |
| `pnpm lint` | Zero warnings |
| `scripts/app.sh smoke` | PASS (dev server on :5173, `/` contains "SplitBook") |

## Reviewer

- **Round 1:** status pass — 1 minor finding: `useMutation` (CreateGroup) vs inline `try/catch` (Login/Register) introduces two mutation patterns. Acceptable as-is; `useMutation` is justified by cache invalidation needs. Convention for future slices: use `useMutation` when cache invalidation is needed, inline `try/catch` when it isn't.

## Scribe

No new lessons added. All findings covered by existing entries (L-H2, L-H11, L-H10). Total LESSONS.md entries: 19 (under 20 cap).

## Open questions deferred to human

1. **GroupDto missing memberCount/netBalance** — Product spec §3.2 says list items show net balance and member count, but OpenAPI GroupDto has neither. Deferred to slice 3 (Group Detail) where balances are needed.
2. **L-H2 violation from interrupted session** — CreateGroup form fields (name, currency, RHF wiring) were written before test-writer was invoked. The test-writer confirmed green immediately for criteria 7-10. Noted in session log.
3. **CreateGroup as modal overlay** — Product spec says "modal or navigates to a form." Chose inline modal overlay (fixed overlay with backdrop) in GroupsList. No dedicated route. Consistent with the existing `onClose` callback pattern.
