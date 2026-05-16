# Slice 1 — Auth: Login + Register

**Date:** 2026-05-16
**Status:** Complete (DoD met)
**Note:** Resumed from interrupted session; reviewer round 2 required fixes for Register error handling, useAuth dead code, untested logout, unused constants, and 401 interceptor edge case.

## Specs in scope

- `specs/slice-plan.md` row 1: Auth — Login + Register
- `specs/product-spec.md` §3.1 (Auth)
- `specs/technical-spec.md` §3.4 (Auth flow), §4 (Routing), §6 (Form handling), §7 (Error handling)

## Endpoints used (from specs/openapi.json)

- POST `/auth/login` — LoginRequest {email, password} → LoginResponse {accessToken, expiresAt} 200
- POST `/auth/register` — RegisterRequest {email, displayName, password} → RegisterResponse {id, email, displayName} 201

## Lessons cited at start

All 18 entries from LESSONS.md read. Key entries applied:
- L-00: Read spec end-to-end
- L-H1: Subagents verify with tools
- L-H2: No component logic before red (violated in interrupted session — noted)
- L-H7: Tests green ≠ app works — smoke the running app
- L-H8: One test per @test-writer invocation
- L-H10: Test-writer touches no shared test infrastructure
- L-H11: New components mirror siblings
- L-FE1: Vitest non-watch only
- L-FE2: pnpm from src/SplitBook.Web
- L-FE3: Parse API responses through Zod
- L-FE6: Testing Library queries from test #1
- L-FE7: Shared infra must be proven by test
- L-FE8: lib/api scaffolded as empty stubs

## Acceptance criteria (from @spec-auditor)

15 criteria produced. All covered by existing tests from the interrupted session, plus 1 new test added for useAuth.logout.

## TDD inner loop

The interrupted session had already written all production code and tests. This session:
1. Verified all 17 tests pass (after fixing App.test.tsx regression)
2. Invoked @spec-auditor for acceptance criteria
3. Invoked @reviewer — found 8 findings (3 major, 5 minor)
4. Fixed all findings:
   - Register.tsx: added try/catch error handling mirroring Login.tsx
   - useAuth.ts: removed dead login/register methods, kept thin isAuthenticated + logout hook
   - useAuth.test.tsx: added logout test, removed unused wrappers
   - api/client.ts: added pathname guard on 401 redirect
   - Login.test.tsx: added window.location mock to suppress jsdom warning
   - lib/constants.ts: deleted unused file
   - App.test.tsx: added token setup + beforeEach clear (L-FE9)
5. Invoked @reviewer round 2 — status: pass (1 minor deferral: untested Register error path)

## DoD verification

| Check | Result |
|-------|--------|
| `pnpm exec vitest run` | 18/18 passed |
| `pnpm build` | Clean (105 modules, 86KB gzipped) |
| `pnpm lint` | Zero warnings |
| `scripts/app.sh smoke` | PASS (dev server on :5173, `/` contains "SplitBook") |

## Reviewer

- **Round 1:** status fail — 8 findings (3 major, 5 minor)
- **Round 2:** status pass — 1 minor deferral (untested Register error path)

## Scribe

- Added L-FE9: Cross-cutting concerns break previous slice tests — fix them proactively
- Total LESSONS.md entries: 19 (under 20 cap)

## Open questions deferred to human

1. Register error path (serverError state) is untested — code mirrors Login.tsx tested pattern exactly; low risk. Can add test in slice 2 cleanup or as quick follow-up.
2. useAuth is now a thin hook (isAuthenticated + logout only). Components call apiRequest directly. This is a conscious decision; future slices may need a richer auth hook (e.g., user info from GET /users/me) — defer to slice 15 (Profile).
