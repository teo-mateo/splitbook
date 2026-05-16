# Slice 4 — Groups: Add + Remove Member

## Date
2026-05-16

## Specs in scope
- `specs/product-spec.md` §3.3 (Group Detail header, members)
- `specs/technical-spec.md` §2 (project layout), §3 (API integration), §6 (form handling)
- `specs/slice-plan.md` — slice 4 row
- `specs/openapi.json` — `POST /groups/{id}/members`, `DELETE /groups/{id}/members/{userId}`

## Lessons cited at start
- L-00 (read spec end-to-end)
- L-H2 (red before green)
- L-H8 (one test per invocation)
- L-H11 (mirror nearest sibling)
- L-FE3 (parse API through Zod)
- L-FE10 (test-writer touches no shared infra)
- L-FE11 (router-param tests need actual Route)
- L-FE12 (atomic values in own elements, use within())

## Acceptance criteria (from @spec-auditor)
1. Group Detail shows "Add member" button in header — ✅
2. Clicking opens AddMember form with email input and submit button — ✅
3. AddMember validates empty email — ✅ (already implemented)
4. AddMember validates email format — ✅ (already implemented)
5. Successful POST /groups/{id}/members with {email}, 204, refetch — ✅
6. 409 shows "User already in this group" — ✅ (already implemented)
7. Cancel button closes form without API call — ✅ (already implemented)
8. Each member has remove control (hidden for single-member groups) — ✅
9. Removing requires destructive confirmation — ✅ (already implemented)
10. Confirmed removal sends DELETE, refetches, member disappears — ✅ (already implemented)
11. Canceling removal does nothing — ✅ (already implemented)
12. API error during removal shows error, member remains — ✅

## Reviewer rounds
- **Round 1:** fail — 2 major (L-FE6 raw DOM traversal in RemoveMember tests), 3 minor (dead code, useConfirm stub, Button stub)
- **Round 2:** pass — all findings resolved

## What the scribe added to LESSONS.md
- Sharpened L-FE12: added explicit `within()` pattern and cited slice 4 as reinforcement

## Files changed
- `src/features/groups/GroupDetail.tsx` — added AddMember button, Remove buttons per member, remove mutation with error handling, error banner
- `src/features/groups/AddMember.tsx` — new: modal form with RHF + zodResolver + TanStack Query mutation
- `src/features/groups/RemoveMember.tsx` — new: confirmation dialog component
- `src/features/groups/AddMember.test.tsx` — new: 7 tests
- `src/features/groups/RemoveMember.test.tsx` — new: 5 tests (rewritten to fix L-FE6)
- `src/api/types.ts` — removed unused AddMemberRequestSchema (dead code cleanup)

## DoD verification
- `pnpm exec vitest run` — 58/58 passed
- `pnpm build` — clean
- `pnpm lint` — clean
- `scripts/app.sh smoke` — PASS

## Open questions deferred to human
- `useConfirm.ts` remains an empty stub; inline modal pattern was chosen. Should the stub be removed?
- GroupDetail has no back/parent nav to GroupsList (Global DoD requirement). Predates slice 4.
- Remove button has no `aria-label` distinguishing which member it targets. Accessibility improvement for future slice.
